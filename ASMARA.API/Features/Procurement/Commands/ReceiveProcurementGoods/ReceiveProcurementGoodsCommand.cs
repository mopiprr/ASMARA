using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Services.Storage;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Procurement.Commands.ReceiveProcurementGoods;

public record ReceiveProcurementGoodsCommand(Guid TicketId, string? Note, IFormFile ProofFile) 
    : IRequest<TransitionResult>;

public class ReceiveProcurementGoodsCommandHandler(
    ITicketFlowService ticketFlow,
    IUnitOfWork uow,
    IMediator mediator,
    ICurrentUser currentUser,
    IFileStorage fileStorage)
    : IRequestHandler<ReceiveProcurementGoodsCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(ReceiveProcurementGoodsCommand command, CancellationToken ct)
    {
        var proofPath = await fileStorage.SaveAsync(command.ProofFile, "procurement-proofs", ct);

        var type = (byte)TicketTypeEnum.Procurement;
        // var ticket = await tickets.Query()
        //     .Include(t => t.Employee)
        //     .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == type, ct);
        var ticket = await uow.Tickets.Query()
            .Include(t => t.Employee)
            .Include(t => t.ProcurementDetails)
                .ThenInclude(d => d.ItemCategory)
            .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == type, ct);

        if (ticket is null)
            throw new NotFoundException($"Procurement ticket {command.TicketId} was not found.");

        TransitionResult result;
        try
        {
            (ticket, result) = await uow.ExecuteInTransactionAsync(async txCt =>
            {
                // 1. Eksekusi transisi alur tiket (Purchasing -> Completed)
                var flowResult = await ticketFlow.ExecuteAsync(command.TicketId, TicketActionEnum.ReceiveGoods, command.Note, txCt);

                // 3. Hitung nomor urut AssetTag terakhir untuk pembuatan aset non-consumable
                var existingTags = await uow.Assets.QueryNoTracking()
                    .Where(a => a.AssetTag.StartsWith("AST-"))
                    .Select(a => a.AssetTag)
                    .ToListAsync(ct);

                var currentMaxTagNum = existingTags
                    .Select(tag => int.TryParse(tag.Replace("AST-", ""), out var num) ? num : 0)
                    .DefaultIfEmpty(0)
                    .Max();

                // 4. Proses setiap barang yang dibeli: update stok inventaris atau buat aset baru
                foreach (var detail in ticket.ProcurementDetails)
                {
                    ItemInventory? inventory = null;
                    if (detail.ItemId.HasValue)
                    {
                        inventory = await uow.ItemInventories.GetByIdAsync(detail.ItemId.Value, txCt);
                    }

                    if (inventory is null)
                    {
                        inventory = await uow.ItemInventories.Query()
    .FirstOrDefaultAsync(
        i => i.ItemCategoryId == detail.ItemCategoryId &&
             i.ItemName.ToLower() == detail.ItemName.ToLower(),
        txCt);

                        if (inventory is null)
                        {
                            var countSameCat = await uow.ItemInventories.QueryNoTracking()
                                .CountAsync(i => i.ItemCategoryId == detail.ItemCategoryId, txCt);
                            var catPrefix = detail.ItemCategory.CategoryCode.Replace("CAT-", "").Replace("DEV-CAT-", "");
                            if (string.IsNullOrWhiteSpace(catPrefix)) catPrefix = "ITM";
                            if (catPrefix.Length > 4) catPrefix = catPrefix[..4];

                            inventory = new ItemInventory
                            {
                                ItemCategoryId = detail.ItemCategoryId,
                                ItemCode = $"{catPrefix}-{(countSameCat + 1):D3}",
                                ItemName = detail.ItemName,
                                IsActive = true,
                                Qty = detail.ItemCategory.TrackingType == (byte)TrackingTypeEnum.Consumable ? 0 : null
                            };
                            uow.ItemInventories.Add(inventory);
                            await uow.SaveChangesAsync(txCt);
                        }

                        detail.ItemId = inventory.Id;
                    }

                    // Periksa TrackingType dari Kategori Barang
                    if (detail.ItemCategory.TrackingType == (byte)TrackingTypeEnum.Consumable)
                    {
                        // A. CONSUMABLE: Tambah stok ke Qty di ItemInventory
                        inventory.Qty = (inventory.Qty ?? 0) + detail.Quantity;
                    }
                    else if (detail.ItemCategory.TrackingType == (byte)TrackingTypeEnum.NonConsumable)
                    {
                        // B. NON-CONSUMABLE: Masukkan ke tabel Asset sejumlah Quantity
                        for (int i = 0; i < detail.Quantity; i++)
                        {
                            currentMaxTagNum++;
                            var newAsset = new Asset
                            {
                                ItemId = inventory.Id,
                                AssetTag = $"AST-{currentMaxTagNum:D6}",
                                Status = (byte)AssetStatusEnum.Available,
                                EmployeeId = null // Masuk ke inventaris gudang sebagai Available
                            };
                            uow.Assets.Add(newAsset);
                        }
                    }
                }

                // 5. Simpan transisi + bukti + stok + aset sekaligus
                await uow.SaveChangesAsync(txCt);

                return (ticket, flowResult);
            }, ct);
        }
        catch
        {
            // Transaksi gagal -> file bukti tidak boleh tertinggal di server.
            fileStorage.Delete(proofPath);
            throw;
        }
        // 6. Kirim notifikasi email ke pemohon bahwa barang telah tiba di gudang
        var actor = await uow.Employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;

        await mediator.Publish(new TicketStatusChangedEvent(
            ticket,
            ticket.Employee,
            actor,
            TicketActionEnum.ReceiveGoods.ToString(),
            command.Note), ct);

        return result;
    }
}
