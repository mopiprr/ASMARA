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

        var assetStatusAvailable = (byte)AssetStatusEnum.Available;

        return await query
            .OrderBy(i => i.ItemCategory.CategoryName)
            .ThenBy(i => i.ItemName)
            .Select(i => new ItemInventoryResponseDto(
                i.Id,
                i.ItemCategoryId,
                i.ItemCategory.CategoryName,
                i.ItemCode,
                i.ItemName,
<<<<<<< Updated upstream
                i.Qty,
                i.IsActive,
                i.ItemCategory.TrackingType,
                i.ItemCategory.TrackingType == (byte)TrackingTypeEnum.NonConsumable
                    ? i.Assets.Count(a => a.Status != (byte)AssetStatusEnum.Disposed
                                       && a.Status != (byte)AssetStatusEnum.Archived)
                    : null,
                i.ItemCategory.TrackingType == (byte)TrackingTypeEnum.NonConsumable
                    ? i.Assets.Count(a => a.Status == (byte)AssetStatusEnum.Available)
                    : null
=======
                i.ItemCategory.TrackingType == (byte)TrackingTypeEnum.NonConsumable
                    ? i.Assets.Count(a => a.Status == assetStatusAvailable)
                    : i.Qty,
                i.IsActive,
                i.ItemCategory.TrackingType,
                i.ItemCategory.TrackingType == (byte)TrackingTypeEnum.NonConsumable ? i.Assets.Count : null,
                i.ItemCategory.TrackingType == (byte)TrackingTypeEnum.NonConsumable ? i.Assets.Count(a => a.Status == assetStatusAvailable) : null
>>>>>>> Stashed changes
            ))
            .ToListAsync(ct);
    }
}
