using ASMARA.API.Common.Paging;
using ASMARA.API.Common.Security;
using ASMARA.API.Features.Catalog.Commands.AssignAsset;
using ASMARA.API.Features.Catalog.Queries.GetAssets;
using ASMARA.API.Features.Catalog.Queries.GetAssetsForMaintenance;
using ASMARA.API.Features.Catalog.Queries.GetAssetsForReplacement;
using ASMARA.API.Features.Catalog.Queries.GetAssetStatistics;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize]
public class AssetsController(ISender sender) : ControllerBase
{
    [HttpGet("available-for-maintenance")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceAssetOptionDto>>> GetAvailableForMaintenance(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAssetsForMaintenanceQuery(), cancellationToken));

    [HttpGet("available-for-replacement")]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<ActionResult<IReadOnlyCollection<ReplacementAssetOptionDto>>> GetAvailableForReplacement([FromQuery] Guid ticketId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAssetsForReplacementQuery(ticketId), cancellationToken));

    [HttpGet]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<ActionResult<PagedResult<AssetListItemDto>>> GetAssets([FromQuery] GetAssetsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    [HttpGet("statistics")]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<ActionResult<AssetStatisticsDto>> GetStatistics(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAssetStatisticsQuery(), cancellationToken));

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<IActionResult> Assign(
       int id, [FromBody] AssignAssetRequestDto request, CancellationToken cancellationToken)
    {
        await sender.Send(new AssignAssetCommand(id, request), cancellationToken);

        return NoContent();
    }
}
