
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

namespace ASMARA.API.Features.Procurement.Commands.ResubmitProcurementTicket;

public record ResubmitProcurementTicketCommand(
    Guid TicketId,
    ResubmitProcurementTicketRequestDto Request)
    : IRequest<TransitionResult>;

public class ResubmitProcurementTicketCommandHandler(
    ITicketRepository tickets,
    IProcurementDetailRepository procurementDetails,
    IEmployeeRepository employees,
    IItemCategoryRepository itemCategories,
    IItemInventoryRepository itemInventories,
    ITicketFlowService ticketFlow,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IMediator mediator)
    : IRequestHandler<ResubmitProcurementTicketCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(
        ResubmitProcurementTicketCommand command,
        CancellationToken ct)
    {
        var ticketId = command.TicketId;
        var request = command.Request;
        var requesterId = currentUser.EmployeeId;

        // 1. Ambil tiket Procurement
        var ticket = await tickets.Query()
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(
                t => t.Id == ticketId &&
                     t.TicketType == (byte)TicketTypeEnum.Procurement,
                ct);

        if (ticket is null)
        {
            throw new NotFoundException(
                $"Tiket pengadaan dengan ID {ticketId} tidak ditemukan.");
        }

        // 2. Validasi kepemilikan tiket
        if (ticket.EmployeeId != requesterId)
        {
            throw new ForbiddenException(
                "Anda hanya dapat mengajukan revisi untuk tiket pengadaan milik Anda sendiri.");
        }

        // 3. Validasi current step
        var currentStep = await tickets.GetCurrentStepAsync(
            ticketId, ct) ?? StepCodes.Created;

        if (currentStep != StepCodes.EmployeeRevision)
        {
            throw new TicketStateException(
                $"Tiket tidak dalam tahap revisi pemohon (status saat ini: '{currentStep}').");
        }

        // 4. Validasi daftar barang
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessRuleException(
                "Minimal harus ada satu barang dalam pengajuan Procurement.");
        }

        var validatedItems = new List<(
            int CategoryId,
            string ItemName,
            int Quantity,
            decimal Price,
            int? ItemId,
            string? ItemCode)>();

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                throw new BusinessRuleException(
                    $"Jumlah barang '{item.ItemName ?? "Item"}' harus lebih besar dari 0.");
            }

            if (item.Price < 0)
            {
                throw new BusinessRuleException(
                    $"Harga estimasi barang '{item.ItemName ?? "Item"}' tidak boleh negatif.");
            }

            // CASE A: Memilih barang dari master inventaris
            if (item.ItemId.HasValue)
            {
                var inventoryItem = await itemInventories.GetByIdAsync(
                    item.ItemId.Value, ct);

                if (inventoryItem is null || !inventoryItem.IsActive)
                {
                    throw new BusinessRuleException(
                        $"Barang inventaris dengan ID {item.ItemId.Value} tidak ditemukan atau sudah tidak aktif.");
                }

                if (!string.IsNullOrWhiteSpace(item.ItemName) &&
                    !item.ItemName.Trim().Equals(
                        inventoryItem.ItemName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessRuleException(
                        $"ItemId {item.ItemId.Value} di inventaris adalah '{inventoryItem.ItemName}', bukan '{item.ItemName}'. " +
                        "Jika Anda bermaksud mengajukan barang baru, set 'itemId' ke null.");
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
            // CASE B: Memasukkan barang manual / baru
            else
            {
                if (string.IsNullOrWhiteSpace(item.ItemName))
                {
                    throw new BusinessRuleException(
                        "Nama barang untuk pengajuan manual wajib diisi.");
                }

                var trimmedName = item.ItemName.Trim();

                var duplicateInventory = await itemInventories.QueryNoTracking()
                    .FirstOrDefaultAsync(
                        i => i.ItemName.ToLower() == trimmedName.ToLower(),
                        ct);

                if (duplicateInventory is not null)
                {
                    throw new BusinessRuleException(
                        $"Barang '{trimmedName}' sudah terdaftar di master inventaris " +
                        $"(Kode: {duplicateInventory.ItemCode}). " +
                        $"Silakan pilih barang tersebut dari inventaris (gunakan ItemId: {duplicateInventory.Id}).");
                }

                if (item.ItemCategoryId <= 0)
                {
                    throw new BusinessRuleException(
                        $"Kategori barang wajib dipilih untuk pengajuan barang manual '{trimmedName}'.");
                }

                var category = await itemCategories.GetByIdAsync(
                    item.ItemCategoryId, ct);

                if (category is null || !category.IsActive)
                {
                    throw new BusinessRuleException(
                        $"Kategori barang dengan ID {item.ItemCategoryId} tidak valid atau tidak aktif.");
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

        // 5. Update tiket dan detail dalam transaksi
        var transactionResult =
            await unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
            {
                var now = DateTime.UtcNow;

                // A. Update metadata tiket
                ticket.Subject = request.Subject.Trim();

                ticket.Description =
                    string.IsNullOrWhiteSpace(request.Description)
                        ? null
                        : request.Description.Trim();

                ticket.UpdatedAt = now;

                // B. Hapus detail lama satu per satu
                var oldDetails = await procurementDetails.Query()
                    .Where(p => p.TicketId == ticket.Id)
                    .ToListAsync(transactionCt);

                foreach (var oldDetail in oldDetails)
                {
                    procurementDetails.Remove(oldDetail);
                }

                // C. Tambahkan detail barang baru
                var newDetails = new List<ProcurementDetail>();

                foreach (var item in validatedItems)
                {
                    var detail = new ProcurementDetail
                    {
                        TicketId = ticket.Id,
                        ItemCategoryId = item.CategoryId,
                        ItemName = item.ItemName,
                        Quantity = item.Quantity,
                        Price = item.Price,
                        ItemId = item.ItemId,
                        CreatedAt = now
                    };

                    procurementDetails.Add(detail);
                    newDetails.Add(detail);
                }

                // D. Jalankan transisi Resubmit
                var resubmitNote =
                    !string.IsNullOrWhiteSpace(request.Note)
                        ? request.Note.Trim()
                        : "Tiket diajukan kembali setelah perbaikan data.";

                var result = await ticketFlow.ExecuteAsync(
                    ticket.Id,
                    TicketActionEnum.Resubmit,
                    resubmitNote,
                    transactionCt);

                await unitOfWork.SaveChangesAsync(transactionCt);

                return (
                    UpdatedDetails: newDetails,
                    FlowResult: result
                );
            }, ct);

        // 6. Kirim notifikasi perubahan status
        var actor = await employees.GetByIdAsync(
            requesterId, ct) ?? ticket.Employee;

        await mediator.Publish(
            new TicketStatusChangedEvent(
                ticket,
                ticket.Employee,
                actor,
                TicketActionEnum.Resubmit.ToString(),
                request.Note,
                items: transactionResult.UpdatedDetails),
            ct);

        return transactionResult.FlowResult;
    }
}
