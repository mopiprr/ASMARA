using ASMARA.API.Models.Inventory;
using MediatR;

namespace ASMARA.API.Features.Catalog.Queries.GetInventoryItems;

public record GetInventoryItemsQuery(
    int? CategoryId = null,
    string? Search = null,
    bool OnlyActive = true
) : IRequest<List<ItemInventoryResponseDto>>;
