using ASMARA.API.Features.Catalog.Queries.GetAssetsForMaintenance;
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
}
