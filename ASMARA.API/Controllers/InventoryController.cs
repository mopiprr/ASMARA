using ASMARA.API.Common.Security;
using ASMARA.API.Features.Catalog.Commands.CreateInventoryItem;
using ASMARA.API.Features.Catalog.Commands.DeleteInventoryItem;
using ASMARA.API.Features.Catalog.Commands.DeleteInventoryUnits;
using ASMARA.API.Features.Catalog.Commands.UpdateInventoryItem;
using ASMARA.API.Features.Catalog.Queries.GetInventoryItemAssets;
using ASMARA.API.Features.Catalog.Queries.GetInventoryItems;
using ASMARA.API.Features.Catalog.Queries.GetItemCategories;
using ASMARA.API.Models.Inventory;
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
    /// Mendukung filter kategori (?categoryId=1), pencarian teks (?search=dell), dan status aktif (?onlyActive=false).
    /// </summary>
    [HttpGet("items")]
    public async Task<ActionResult<List<ItemInventoryResponseDto>>> GetItems(
        [FromQuery] int? categoryId,
        [FromQuery] string? search,
        [FromQuery] bool onlyActive = false,
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

    /// <summary>
    /// Mengambil seluruh unit fisik aset individual dari suatu barang Non-Consumable (AssetTag, status, pemegang).
    /// </summary>
    [HttpGet("items/{id:int}/assets")]
    [Authorize(Roles = $"{Roles.Inventory},{Roles.Admin}")]
    public async Task<ActionResult<List<InventoryItemAssetDto>>> GetItemAssets(
        int id,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetInventoryItemAssetsQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Menambah barang baru ke master inventaris.
    /// Jika kategori bertipe Non-Consumable, sistem otomatis men-generate Asset Tag (AST-XXXXXX) sejumlah initialStock.
    /// </summary>
    [HttpPost("items")]
    [Authorize(Roles = $"{Roles.Inventory},{Roles.Admin}")]
    public async Task<ActionResult<ItemInventoryResponseDto>> CreateItem(
        [FromBody] CreateInventoryItemRequestDto dto,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new CreateInventoryItemCommand(dto.ItemCategoryId, dto.ItemName, dto.InitialStock), ct);
        return CreatedAtAction(nameof(GetItems), new { search = result.ItemCode }, result);
    }

    /// <summary>
    /// Mengubah nama, kategori, atau jumlah stok barang inventaris.
    /// Untuk Non-Consumable, penambahan/pengurangan stok akan disinkronkan dengan entri aset di tabel Asset.
    /// </summary>
    [HttpPut("items/{id:int}")]
    [Authorize(Roles = $"{Roles.Inventory},{Roles.Admin}")]
    public async Task<ActionResult<ItemInventoryResponseDto>> UpdateItem(
        int id,
        [FromBody] UpdateInventoryItemRequestDto dto,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new UpdateInventoryItemCommand(id, dto.ItemName, dto.ItemCategoryId, dto.NewStock, dto.IsActive), ct);
        return Ok(result);
    }

    /// <summary>
    /// Mengurangi kuantitas stok barang Consumable atau menghapus unit aset spesifik pada barang Non-Consumable.
    /// Unit aset yang dihapus otomatis dicabut dari tabel Asset sehingga tidak lagi muncul di halaman /assets.
    /// </summary>
    [HttpPost("items/{id:int}/delete-units")]
    [Authorize(Roles = $"{Roles.Inventory},{Roles.Admin}")]
    public async Task<ActionResult<DeleteInventoryUnitsResultDto>> DeleteUnits(
        int id,
        [FromBody] DeleteInventoryUnitsRequestDto dto,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new DeleteInventoryUnitsCommand(id, dto), ct);
        return Ok(result);
    }

    /// <summary>
    /// Menonaktifkan (soft delete) barang inventaris.
    /// Gagal jika masih terdapat aset yang sedang digunakan (in-use) oleh karyawan.
    /// </summary>
    [HttpDelete("items/{id:int}")]
    [Authorize(Roles = $"{Roles.Inventory},{Roles.Admin}")]
    public async Task<IActionResult> DeleteItem(
        int id,
        CancellationToken ct = default)
    {
        var success = await mediator.Send(new DeleteInventoryItemCommand(id), ct);
        return Ok(new { success, message = "Item inventory successfully deactivated." });
    }
}
