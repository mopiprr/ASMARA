using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Common;

/// Membangun tiket child Procurement + itemnya (belum di-Add sebagai tiket; itemnya sudah di-Add).
/// WAJIB dipanggil di dalam transaksi (nomor tiket dikunci sampai commit).
public static class ChildProcurementBuilder
{
    public static async Task<Ticket> BuildAsync(
        ITicketRepository tickets,
        IProcurementDetailRepository procurementDetails,
        IItemInventoryRepository itemInventories,
        IItemCategoryRepository itemCategories,
        string subject,
        string? description,
        IEnumerable<CreateProcurementItemRequest> items,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var child = new Ticket
        {
            Id = Guid.CreateVersion7(),
            TicketNumber = await tickets.GenerateTicketNumberAsync(TicketTypeEnum.Procurement, ct),
            TicketType = (byte)TicketTypeEnum.Procurement,
            Subject = subject.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = now
        };

        foreach (var item in items)
        {
            int categoryId;
            string itemName;

            if (item.ItemId is int itemId)
            {
                var catalog = await itemInventories.QueryNoTracking()
                    .Where(i => i.Id == itemId && i.IsActive)
                    .Select(i => new { i.ItemCategoryId, i.ItemName })
                    .FirstOrDefaultAsync(ct)
                    ?? throw new BusinessRuleException($"Item {itemId} was not found or inactive.");
                categoryId = catalog.ItemCategoryId;
                itemName = catalog.ItemName;
            }
            else
            {
                if (!await itemCategories.QueryNoTracking().AnyAsync(c => c.Id == item.ItemCategoryId, ct))
                    throw new BusinessRuleException($"Item category {item.ItemCategoryId} was not found.");
                categoryId = item.ItemCategoryId;
                itemName = item.ItemName.Trim();
            }

            procurementDetails.Add(new ProcurementDetail
            {
                TicketId = child.Id,
                ItemId = item.ItemId,
                ItemCategoryId = categoryId,
                ItemName = itemName,
                Quantity = item.Quantity,
                Price = item.Price,
                CreatedAt = now
            });
        }

        return child;
    }
}