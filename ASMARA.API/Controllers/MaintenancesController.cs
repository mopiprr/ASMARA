using ASMARA.API.Enums;
using ASMARA.API.Features.Maintenance.Commands.AssignHandler;
using ASMARA.API.Features.Maintenance.Commands.CreateMaintenanceTicket;
using ASMARA.API.Features.Tickets.Commands.ExecuteTicketAction;
using ASMARA.API.Features.Tickets.Queries.GetTicketDetail;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<TicketResponseDto>> Approve(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TicketNoteRequestDto? request,
       CancellationToken ct)
    {
        await sender.Send(new ExecuteTicketActionCommand(id, TicketTypeEnum.Maintenance, TicketActionEnum.Approve, request?.Note), ct);

        return Ok(await sender.Send(new GetTicketByIdQuery(id), ct));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<TicketResponseDto>> Reject(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TicketNoteRequestDto? request,
       CancellationToken ct)
    {
        await sender.Send(new ExecuteTicketActionCommand(id, TicketTypeEnum.Maintenance, TicketActionEnum.Reject, request?.Note), ct);

        return Ok(await sender.Send(new GetTicketByIdQuery(id), ct));
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<TransitionResult>> Assign(
        Guid id,
        [FromBody] AssignTechnicianDto request,
        CancellationToken ct)
    {
        var result = await sender.Send(new AssignMaintenanceHandlerCommand(id, request), ct);
        return Ok(result);
    }
}
