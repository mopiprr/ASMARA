using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Queries.GetInventoryItems;

public record GetInventoryItemsQuery(
    int? CategoryId = null,
    string? Search = null,
    bool OnlyActive = true
) : IRequest<List<ItemInventoryResponseDto>>;

public class GetInventoryItemsQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetInventoryItemsQuery, List<ItemInventoryResponseDto>>
{
    public async Task<List<ItemInventoryResponseDto>> Handle(GetInventoryItemsQuery request, CancellationToken ct)
    {
        var query = uow.ItemInventories.QueryNoTracking()
            .Include(i => i.ItemCategory)
            .AsQueryable();

        // Hanya mengambil barang yang IsActive = true
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

        const byte consumable = (byte)TrackingTypeEnum.Consumable;
        const byte availableStatus = (byte)AssetStatusEnum.Available;

        return await query
            .OrderBy(i => i.ItemCategory.CategoryName)
            .ThenBy(i => i.ItemName)
            .Select(i => new ItemInventoryResponseDto(
                i.Id,
                i.ItemCategoryId,
                i.ItemCategory.CategoryName,
                i.ItemCode,
                i.ItemName,
                i.ItemCategory.TrackingType == consumable
                    ? (i.Qty ?? 0)
                    : i.Assets.Count(a => a.Status == availableStatus),
                i.IsActive,
                i.ItemCategory.TrackingType,
                i.ItemCategory.TrackingType == consumable
                    ? null
                    : (int?)i.Assets.Count(),
                i.ItemCategory.TrackingType == consumable
                    ? null
                    : (int?)i.Assets.Count(a => a.Status == availableStatus)
            ))
            .ToListAsync(ct);
    }
}
