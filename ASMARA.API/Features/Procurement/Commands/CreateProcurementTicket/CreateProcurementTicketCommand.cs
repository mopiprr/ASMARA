using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Procurement.Commands.CreateProcurementTicket;

public record CreateProcurementTicketCommand(
    Guid EmployeeId,
    CreateTicketProcurementDto Request
) : IRequest<TicketResponseDto>;

public class CreateProcurementTicketCommandHandler(
    ITicketRepository tickets,
    IProcurementDetailRepository procurementDetails,
    IEmployeeRepository employees,
    IItemCategoryRepository itemCategories,
    IItemInventoryRepository itemInventories,
    ITicketFlowService ticketFlow,
    IUnitOfWork unitOfWork,
    IMediator mediator,
    ILogger<CreateProcurementTicketCommandHandler> logger) : IRequestHandler<CreateProcurementTicketCommand, TicketResponseDto>
{
    public async Task<TicketResponseDto> Handle(CreateProcurementTicketCommand command, CancellationToken ct)
    {
        var request = command.Request;

        // 1. Validasi Subject & Items Dasar
        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new BusinessRuleException("Judul pengajuan (Subject) wajib diisi.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessRuleException("Daftar barang pengadaan minimal harus memiliki 1 item.");
        }

        // List untuk menampung data item yang telah divalidasi dan dinormalisasi
        var validatedItems = new List<(int CategoryId, string ItemName, int Quantity, decimal Price, int? ItemId, string? ItemCode)>();

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                throw new BusinessRuleException($"Jumlah barang '{item.ItemName ?? "Item"}' harus lebih besar dari 0.");
            }

            if (item.Price < 0)
            {
                throw new BusinessRuleException($"Harga estimasi barang '{item.ItemName ?? "Item"}' tidak boleh negatif.");
            }

            // KASUS A: Memilih barang yang sudah ada di inventaris (ItemId terisi)
            if (item.ItemId.HasValue)
            {
                var inventoryItem = await itemInventories.GetByIdAsync(item.ItemId.Value, ct);
                if (inventoryItem is null || !inventoryItem.IsActive)
                {
                    throw new BusinessRuleException($"Barang inventaris dengan ID {item.ItemId.Value} tidak ditemukan atau sudah tidak aktif.");
                }

                // Proteksi jika nama yang diinput berbeda dengan nama master inventaris
                if (!string.IsNullOrWhiteSpace(item.ItemName) && 
                    !item.ItemName.Trim().Equals(inventoryItem.ItemName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessRuleException(
                        $"ItemId {item.ItemId.Value} di inventaris adalah '{inventoryItem.ItemName}', bukan '{item.ItemName}'. " +
                        $"Jika Anda bermaksud mengajukan barang baru, set 'itemId' ke null. " +
                        $"Jika bermaksud mengajukan barang inventaris, pilih dari katalog atau gunakan nama '{inventoryItem.ItemName}'.");
                }

                validatedItems.Add((
                    CategoryId: inventoryItem.ItemCategoryId,
                    ItemName: inventoryItem.ItemName,
                    Quantity: item.Quantity,
                    Price: item.Price,
                    ItemId: inventoryItem.Id,
                    ItemCode: inventoryItem.ItemCode
                ));
            }
            // KASUS B: Memasukkan barang manual / baru (ItemId bernilai null)
            else
            {
                if (string.IsNullOrWhiteSpace(item.ItemName))
                {
                    throw new BusinessRuleException("Nama barang untuk pengajuan manual wajib diisi.");
                }

                var trimmedName = item.ItemName.Trim();

                // VALIDASI ANTI-DUPLIKASI:
                // Cek apakah nama barang sudah ada di master ItemInventory
                var duplicateInventory = await itemInventories.QueryNoTracking()
                    .FirstOrDefaultAsync(i => i.ItemName.ToLower() == trimmedName.ToLower(), ct);

                if (duplicateInventory is not null)
                {
                    throw new BusinessRuleException(
                        $"Barang '{trimmedName}' sudah terdaftar di master inventaris (Kode: {duplicateInventory.ItemCode}). " +
                        $"Silakan pilih barang tersebut dari inventaris (gunakan ItemId: {duplicateInventory.Id}) agar tidak terjadi duplikasi.");
                }

                if (!item.ItemCategoryId.HasValue)
                {
                    throw new BusinessRuleException($"Kategori barang wajib dipilih untuk pengajuan barang manual '{trimmedName}'.");
                }

                var category = await itemCategories.GetByIdAsync(item.ItemCategoryId.Value, ct);
                if (category is null || !category.IsActive)
                {
                    throw new BusinessRuleException($"Kategori barang dengan ID {item.ItemCategoryId.Value} tidak valid atau tidak aktif.");
                }

                validatedItems.Add((
                    CategoryId: category.Id,
                    ItemName: trimmedName,
                    Quantity: item.Quantity,
                    Price: item.Price,
                    ItemId: null,
                    ItemCode: null
                ));
            }
        }

        // 2. Generate Nomor Tiket Unik (Format: PRC-yyMMdd-XXXX, max 20 karakter)
        var now = DateTime.UtcNow;
        string ticketNumber;
        do
        {
            ticketNumber = $"PRC-{now:yyMMdd}-{Random.Shared.Next(1000, 9999)}";
        } while (await tickets.QueryNoTracking().AnyAsync(t => t.TicketNumber == ticketNumber, ct));

        // 3. Buat Entitas Ticket & ProcurementDetails
        var ticket = new Ticket
        {
            Id = Guid.CreateVersion7(),
            EmployeeId = command.EmployeeId,
            TicketNumber = ticketNumber,
            TicketType = (byte)TicketTypeEnum.Procurement,
            Subject = request.Subject.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ParentTicketId = request.ParentTicketId,
            CreatedAt = now
        };

        var detailsList = new List<ProcurementDetail>();
        foreach (var v in validatedItems)
        {
            var detail = new ProcurementDetail
            {
                TicketId = ticket.Id,
                ItemCategoryId = v.CategoryId,
                ItemName = v.ItemName,
                Quantity = v.Quantity,
                Price = v.Price,
                ItemId = v.ItemId,
                CreatedAt = now
            };
            procurementDetails.Add(detail);
            detailsList.Add(detail);
        }

        tickets.Add(ticket);

        // 4. Jalankan TicketFlow Engine (Action: Create -> UnderReview & catat riwayat ke TicketWorkflowLog)
        var logNote = !string.IsNullOrWhiteSpace(request.Description)
            ? request.Description.Trim()
            : $"Pengajuan pengadaan: {request.Subject.Trim()}";

        var flowResult = await ticketFlow.ExecuteAsync(
            ticket.Id,
            TicketActionEnum.Create,
            logNote,
            ct);

        // 5. Simpan Perubahan ke Database
        await unitOfWork.SaveChangesAsync(ct);

        // 6. Opsi 2: Kirim Notifikasi Email setelah simpan DB sukses
        var requester = await employees.GetByIdAsync(ticket.EmployeeId, ct);
        Employee? manager = null;
        if (flowResult.NextTaskEmployeeId.HasValue)
        {
            manager = await employees.GetByIdAsync(flowResult.NextTaskEmployeeId.Value, ct);
        }
        else if (requester?.ManagerId is not null)
        {
            manager = await employees.GetByIdAsync(requester.ManagerId.Value, ct);
        }

        if (requester is not null)
        {
            await mediator.Publish(new TicketCreatedEvent(ticket, requester, manager, detailsList), ct);
        }

        // Catat ke Application Log (Serilog Console & File)
        logger.LogInformation(
            "[Procurement] Tiket {TicketNumber} (ID: {TicketId}) berhasil dibuat oleh {EmployeeName} ({EmployeeId}) dengan {ItemCount} item. Status: {FromStep} -> {ToStep}. Ditugaskan ke: {NextTaskEmployeeId}",
            ticket.TicketNumber, ticket.Id, requester?.FullName ?? "Unknown", ticket.EmployeeId, detailsList.Count, flowResult.FromStep, flowResult.ToStep, flowResult.NextTaskEmployeeId);

        // 7. Siapkan Response DTO
        var categoryIds = detailsList.Select(d => d.ItemCategoryId).Distinct().ToList();
        var categoriesMap = await itemCategories.QueryNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CategoryName, ct);

        var itemCodesMap = validatedItems
            .Where(v => v.ItemId.HasValue && v.ItemCode is not null)
            .GroupBy(v => v.ItemId!.Value)
            .ToDictionary(g => g.Key, g => g.First().ItemCode);

        var detailDtos = detailsList.Select(d => new ProcurementDetailResponseDto(
            TicketId: d.TicketId,
            ItemId: d.ItemId,
            ItemCode: d.ItemId.HasValue ? itemCodesMap.GetValueOrDefault(d.ItemId.Value) : null,
            ItemCategoryId: d.ItemCategoryId,
            CategoryName: categoriesMap.GetValueOrDefault(d.ItemCategoryId, "Unknown"),
            ItemName: d.ItemName,
            Quantity: d.Quantity,
            Price: d.Price,
            TotalPrice: d.Quantity * d.Price,
            CreatedAt: d.CreatedAt
        )).ToList();

        return new TicketResponseDto(
            Id: ticket.Id,
            ParentTicketId: ticket.ParentTicketId,
            EmployeeId: ticket.EmployeeId,
            EmployeeName: requester?.FullName,
            TicketNumber: ticket.TicketNumber,
            TicketType: ticket.TicketType,
            Subject: ticket.Subject,
            Description: ticket.Description,
            AssignedToEmployeeId: ticket.AssignedToEmployeeId,
            AssignedTo: ticket.AssignedTo,
            CreatedAt: ticket.CreatedAt,
            ClosedAt: ticket.ClosedAt,
            UpdatedAt: ticket.UpdatedAt,
            CurrentTaskEmployeeId: ticket.CurrentTaskEmployeeId,
            CurrentStep: flowResult.ToStep,
            MaintenanceDetail: null,
            ProcurementDetails: detailDtos
        );
    }
}
