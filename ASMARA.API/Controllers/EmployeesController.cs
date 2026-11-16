using ASMARA.API.Common.Paging;
using ASMARA.API.Common.Security;
using ASMARA.API.Features.Employees.Commands.CreateEmployee;
using ASMARA.API.Features.Employees.Queries.GetEmployeeById;
using ASMARA.API.Features.Employees.Queries.GetEmployeeOptions;
using ASMARA.API.Features.Employees.Queries.GetEmployees;
using ASMARA.API.Features.Employees.Queries.GetMaintenanceHandlers;
using ASMARA.API.Features.Employees.Queries.GetManagerOptions;
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

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PagedResult<EmployeeListItemDto>>> GetEmployees([FromQuery] GetEmployeesQuery query, CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    [HttpGet("managers")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<IReadOnlyList<ManagerOptionDto>>> GetManagerOptions([FromQuery] Guid departmentId, CancellationToken ct)
        => Ok(await sender.Send(new GetManagerOptionsQuery(departmentId), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<EmployeeDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetEmployeeByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<EmployeeDetailDto>> Create([FromBody] CreateEmployeeRequestDto request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateEmployeeCommand(request), ct);
        var employee = await sender.Send(new GetEmployeeByIdQuery(id), ct);

        return CreatedAtAction(nameof(GetById), new { id }, employee);
    }

    [HttpGet("options")]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<ActionResult<IReadOnlyList<EmployeeOptionDto>>> GetEmployeeOptions(CancellationToken ct)
        => Ok(await sender.Send(new GetEmployeeOptionsQuery(), ct));
}