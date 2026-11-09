using ASMARA.API.Common.Security;
using ASMARA.API.Features.Auth.Queries.GetRoles;
using ASMARA.API.Models.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Roles = Roles.Admin)]
public class RolesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleOptionDto>>> GetRoles(CancellationToken ct)
        => Ok(await sender.Send(new GetRolesQuery(), ct));
}