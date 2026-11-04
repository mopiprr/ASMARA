using ASMARA.API.Common.Security;
using ASMARA.API.Features.Procurement.Commands.CreateProcurementTicket;
using ASMARA.API.Models.Ticket;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/procurement")]
[Authorize]
public class ProcurementsController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTicketProcurementDto request,
        CancellationToken ct)
    {
        var command = new CreateProcurementTicketCommand(currentUser.EmployeeId, request);
        var result = await mediator.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
