using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Procurement.Commands.CreateProcurementTicket;

public record CreateProcurementTicketCommand(CreateTicketProcurementDto Request) : IRequest<Guid>;

public class CreateProcurementTicketCommandHandler(
    ITicketRepository tickets,
    IProcurementDetailRepository procurementDetails,
    IEmployeeRepository employees,
    IItemCategoryRepository itemCategories,
    IItemInventoryRepository itemInventories,
    ITicketFlowService ticketFlow,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IMediator mediator,
    ILogger<CreateProcurementTicketCommandHandler> logger) : IRequestHandler<CreateProcurementTicketCommand, Guid>
{
    public async Task<Guid> Handle(CreateProcurementTicketCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var requesterId = currentUser.EmployeeId;

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

        // Eksekusi transaksi pembuatan tiket dan penomoran urut
        var (ticket, detailsList, flowResult) = await unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            // 2. Generate Nomor Tiket Urut Harian dari Repository (Format: PRC-ddMMyy-XXX, reset harian)
            var ticketNumber = await tickets.GenerateTicketNumberAsync(TicketTypeEnum.Procurement, transactionCt);

            // 3. Buat Entitas Ticket & ProcurementDetails
            var now = DateTime.UtcNow;
            var newTicket = new Ticket
            {
                Id = Guid.CreateVersion7(),
                EmployeeId = requesterId,
                TicketNumber = ticketNumber,
                TicketType = (byte)TicketTypeEnum.Procurement,
                Subject = request.Subject.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                ParentTicketId = request.ParentTicketId,
                CreatedAt = now
            };

            var details = new List<ProcurementDetail>();
            foreach (var v in validatedItems)
            {
                var detail = new ProcurementDetail
                {
                    TicketId = newTicket.Id,
                    ItemCategoryId = v.CategoryId,
                    ItemName = v.ItemName,
                    Quantity = v.Quantity,
                    Price = v.Price,
                    ItemId = v.ItemId,
                    CreatedAt = now
                };
                procurementDetails.Add(detail);
                details.Add(detail);
            }

            tickets.Add(newTicket);

            // 4. Jalankan TicketFlow Engine (Action: Create -> UnderReview & catat riwayat ke TicketWorkflowLog)
            var logNote = !string.IsNullOrWhiteSpace(request.Description)
                ? request.Description.Trim()
                : $"Pengajuan pengadaan: {request.Subject.Trim()}";

            var result = await ticketFlow.ExecuteAsync(
                newTicket.Id,
                TicketActionEnum.Create,
                logNote,
                transactionCt);

            // 5. Simpan Perubahan ke Database
            await unitOfWork.SaveChangesAsync(transactionCt);

            return (newTicket, details, result);
        }, ct);

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

        // Catat ke Log
        logger.LogInformation(
            "[Procurement] Tiket {TicketNumber} (ID: {TicketId}) berhasil dibuat oleh {EmployeeName} ({EmployeeId}) dengan {ItemCount} item. Status: {FromStep} -> {ToStep}. Ditugaskan ke: {NextTaskEmployeeId}",
            ticket.TicketNumber, ticket.Id, requester?.FullName ?? "Unknown", ticket.EmployeeId, detailsList.Count, flowResult.FromStep, flowResult.ToStep, flowResult.NextTaskEmployeeId);
        
        return ticket.Id;
    }
}
