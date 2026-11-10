using ASMARA.API.Common.Paging;
using ASMARA.API.Common.Security;
using ASMARA.API.Features.Catalog.Queries.GetAssets;
using ASMARA.API.Features.Catalog.Queries.GetAssetsForMaintenance;
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

    [HttpGet]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<ActionResult<PagedResult<AssetListItemDto>>> GetAssets([FromQuery] GetAssetsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    [HttpGet("statistics")]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<ActionResult<AssetStatisticsDto>> GetStatistics(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAssetStatisticsQuery(), cancellationToken));
}
