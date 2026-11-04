using ASMARA.API.Models.Inventory;
using MediatR;

namespace ASMARA.API.Features.Catalog.Queries.GetItemCategories;

public record GetItemCategoriesQuery(bool OnlyActive = true) : IRequest<List<ItemCategoryResponseDto>>;
