using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Queries.GetItemCategories;

public record GetItemCategoriesQuery(bool OnlyActive = true) : IRequest<List<ItemCategoryResponseDto>>;

public class GetItemCategoriesQueryHandler(IItemCategoryRepository itemCategories)
    : IRequestHandler<GetItemCategoriesQuery, List<ItemCategoryResponseDto>>
{
    public async Task<List<ItemCategoryResponseDto>> Handle(GetItemCategoriesQuery request, CancellationToken ct)
    {
        var query = itemCategories.QueryNoTracking().AsQueryable();

        // Hanya mengambil kategori yang IsActive = true
        if (request.OnlyActive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.CategoryName)
            .Select(c => new ItemCategoryResponseDto(
                c.Id,
                c.CategoryCode,
                c.CategoryName,
                c.TrackingType,
                c.IsActive
            ))
            .ToListAsync(ct);
    }
}
