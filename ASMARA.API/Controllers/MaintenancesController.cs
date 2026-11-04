using ASMARA.API.Features.Maintenance.Commands.CreateMaintenanceTicket;
using ASMARA.API.Features.Tickets.Queries.GetTicketDetail;
using ASMARA.API.Models.Ticket;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/maintenances")]
[Authorize]
public class MaintenancesController(ISender sender) : ControllerBase
{

    [HttpPost]
    public async Task<ActionResult<TicketResponseDto>> Create([FromBody] CreateMaintenanceTicketRequestDto request, CancellationToken ct)
    {
        var id = await sender.Send(new CreateMaintenanceTicketCommand(request), ct);
        var ticket = await sender.Send(new GetTicketByIdQuery(id), ct);

        return CreatedAtAction(nameof(TicketsController.GetById), "Tickets", new { id }, ticket);
    }
}
