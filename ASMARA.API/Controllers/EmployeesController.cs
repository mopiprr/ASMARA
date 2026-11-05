using ASMARA.API.Common.Security;
using ASMARA.API.Features.Employees.Queries.GetMaintenanceHandlers;
using ASMARA.API.Models.Organization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController(ISender sender) : ControllerBase
{
    [HttpGet("handlers")]
    [Authorize(Roles = Roles.ItSupport)]
    public async Task<ActionResult<IReadOnlyList<HandlerOptionDto>>> GetHandlers(CancellationToken ct)
        => Ok(await sender.Send(new GetMaintenanceHandlersQuery(), ct));
}