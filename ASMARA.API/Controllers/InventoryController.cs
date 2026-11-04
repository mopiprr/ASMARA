using ASMARA.API.Features.Catalog.Queries.GetInventoryItems;
using ASMARA.API.Features.Catalog.Queries.GetItemCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Mengambil seluruh data barang inventaris (ItemInventory).
    /// Mendukung filter kategori (?categoryId=1) dan pencarian teks (?search=dell).
    /// </summary>
    [HttpGet("items")]
    public async Task<IActionResult> GetItems(
        [FromQuery] int? categoryId,
        [FromQuery] string? search,
        [FromQuery] bool onlyActive = true,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetInventoryItemsQuery(categoryId, search, onlyActive), ct);
        return Ok(result);
    }

    /// <summary>
    /// Mengambil seluruh kategori barang (ItemCategory) untuk dropdown input barang manual.
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(
        [FromQuery] bool onlyActive = true,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetItemCategoriesQuery(onlyActive), ct);
        return Ok(result);
    }
}
