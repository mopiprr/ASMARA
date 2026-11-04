using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Queries.GetInventoryItems;

public class GetInventoryItemsQueryHandler(IItemInventoryRepository itemInventories)
    : IRequestHandler<GetInventoryItemsQuery, List<ItemInventoryResponseDto>>
{
    public async Task<List<ItemInventoryResponseDto>> Handle(GetInventoryItemsQuery request, CancellationToken ct)
    {
        var query = itemInventories.QueryNoTracking()
            .Include(i => i.ItemCategory)
            .AsQueryable();

        if (request.OnlyActive)
        {
            query = query.Where(i => i.IsActive);
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(i => i.ItemCategoryId == request.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(i => i.ItemName.ToLower().Contains(term) || i.ItemCode.ToLower().Contains(term));
        }

        return await query
            .OrderBy(i => i.ItemCategory.CategoryName)
            .ThenBy(i => i.ItemName)
            .Select(i => new ItemInventoryResponseDto(
                i.Id,
                i.ItemCategoryId,
                i.ItemCategory.CategoryName,
                i.ItemCode,
                i.ItemName,
                i.Qty,
                i.IsActive
            ))
            .ToListAsync(ct);
    }
}
