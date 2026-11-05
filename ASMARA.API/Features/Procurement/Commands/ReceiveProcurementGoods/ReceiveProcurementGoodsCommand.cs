using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Procurement.Commands.ReceiveProcurementGoods;

public record ReceiveProcurementGoodsCommand(Guid TicketId, string? Note) 
    : IRequest<TransitionResult>;

// public class ReceiveProcurementGoodsCommandHandler(
//     ITicketRepository tickets,
//     ITicketFlowService ticketFlow,
//     IUnitOfWork uow,
//     IMediator mediator,
//     IEmployeeRepository employees,
//     ICurrentUser currentUser)
//     : IRequestHandler<ReceiveProcurementGoodsCommand, TransitionResult>
public class ReceiveProcurementGoodsCommandHandler(
    ITicketRepository tickets,
    ITicketFlowService ticketFlow,
    IUnitOfWork uow,
    IMediator mediator,
    IEmployeeRepository employees,
    IItemInventoryRepository itemInventories,
    IAssetRepository assets,
    ICurrentUser currentUser)
    : IRequestHandler<ReceiveProcurementGoodsCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(ReceiveProcurementGoodsCommand command, CancellationToken ct)
    {
        var type = (byte)TicketTypeEnum.Procurement;
        // var ticket = await tickets.Query()
        //     .Include(t => t.Employee)
        //     .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == type, ct);
        var ticket = await tickets.Query()
            .Include(t => t.Employee)
            .Include(t => t.ProcurementDetails)
                .ThenInclude(d => d.ItemCategory)
            .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == type, ct);

        if (ticket is null)
            throw new NotFoundException($"Procurement ticket {command.TicketId} was not found.");

        // 1. Eksekusi transisi alur tiket (Purchasing -> Completed)
        var result = await ticketFlow.ExecuteAsync(command.TicketId, TicketActionEnum.ReceiveGoods, command.Note, ct);

        // 2. Hitung nomor urut AssetTag terakhir untuk pembuatan aset non-consumable
        var existingTags = await assets.QueryNoTracking()
            .Where(a => a.AssetTag.StartsWith("AST-"))
            .Select(a => a.AssetTag)
            .ToListAsync(ct);

        var currentMaxTagNum = existingTags
            .Select(tag => int.TryParse(tag.Replace("AST-", ""), out var num) ? num : 0)
            .DefaultIfEmpty(0)
            .Max();

        // 3. Proses setiap barang yang dibeli: update stok inventaris atau buat aset baru
        foreach (var detail in ticket.ProcurementDetails)
        {
            ItemInventory? inventory = null;
            if (detail.ItemId.HasValue)
            {
                inventory = await itemInventories.GetByIdAsync(detail.ItemId.Value, ct);
            }

            if (inventory is null)
            {
                inventory = await itemInventories.Query()
                    .FirstOrDefaultAsync(i => i.ItemName.ToLower() == detail.ItemName.ToLower(), ct);

                if (inventory is null)
                {
                    var countSameCat = await itemInventories.QueryNoTracking()
                        .CountAsync(i => i.ItemCategoryId == detail.ItemCategoryId, ct);
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
                    itemInventories.Add(inventory);
                    await uow.SaveChangesAsync(ct);
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
                    assets.Add(newAsset);
                }
            }
        }

        // 4. Simpan seluruh perubahan stok dan aset ke Database
        await uow.SaveChangesAsync(ct);

        // 5. Kirim notifikasi email ke pemohon bahwa barang telah tiba di gudang
        var actor = await employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;

        await mediator.Publish(new TicketStatusChangedEvent(
            ticket,
            ticket.Employee,
            actor,
            TicketActionEnum.ReceiveGoods.ToString(),
            command.Note), ct);

        return result;
    }
}
