using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Queries.GetSpareParts;

public record GetSparePartsQuery(
    string? Search = null,
    bool OnlyInStock = false
) : IRequest<List<SparePartItemDto>>;

public class GetSparePartsQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetSparePartsQuery, List<SparePartItemDto>>
{
    public async Task<List<SparePartItemDto>> Handle(GetSparePartsQuery request, CancellationToken ct)
    {
        var query = uow.ItemInventories.QueryNoTracking()
            .Include(i => i.ItemCategory)
            .Where(i => i.IsActive && i.ItemCategory.IsActive && i.ItemCategory.TrackingType == (byte)TrackingTypeEnum.Consumable);

        if (request.OnlyInStock)
        {
            query = query.Where(i => i.Qty > 0);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(i => i.ItemName.ToLower().Contains(term) || i.ItemCode.ToLower().Contains(term));
        }

        return await query
            .OrderBy(i => i.ItemCategory.CategoryName)
            .ThenBy(i => i.ItemName)
            .Select(i => new SparePartItemDto(
                i.Id,
                i.ItemCategoryId,
                i.ItemCategory.CategoryName,
                i.ItemCode,
                i.ItemName,
                i.Qty ?? 0,
                i.IsActive
            ))
            .ToListAsync(ct);
    }
}
