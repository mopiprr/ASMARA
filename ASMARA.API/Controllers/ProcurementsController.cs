using ASMARA.API.Enums;
using ASMARA.API.Features.Procurement.Commands.CreateProcurementTicket;
using ASMARA.API.Features.Procurement.Commands.ReceiveProcurementGoods;
using ASMARA.API.Features.Procurement.Commands.ResubmitProcurementTicket;
using ASMARA.API.Features.Tickets.Commands.ExecuteTicketAction;
using ASMARA.API.Features.Tickets.Queries.GetTicketDetail;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/procurements")]
[Authorize]
public class ProcurementsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TicketResponseDto>> Create(
        // [FromBody] CreateTicketProcurementDto request,
        [FromBody] CreateProcurementTicketRequestDto request,
        CancellationToken ct)
    {
        var id = await sender.Send(new CreateProcurementTicketCommand(request), ct);
        var ticket = await sender.Send(new GetTicketByIdQuery(id), ct);

        return CreatedAtAction(nameof(TicketsController.GetById), "Tickets", new { id }, ticket);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<TransitionResult>> Approve(
        Guid id,
        [FromBody] TicketNoteRequestDto? request,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ExecuteTicketActionCommand(id, TicketTypeEnum.Procurement, TicketActionEnum.Approve, request?.Note),
            ct);

        return Ok(result);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<TransitionResult>> Reject(
        Guid id,
        [FromBody] TicketNoteRequestDto? request,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ExecuteTicketActionCommand(id, TicketTypeEnum.Procurement, TicketActionEnum.Reject, request?.Note),
            ct);

        return Ok(result);
    }

    [HttpPost("{id:guid}/revise")]
    public async Task<ActionResult<TransitionResult>> Revise(
        Guid id,
        [FromBody] TicketNoteRequestDto? request,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ExecuteTicketActionCommand(id, TicketTypeEnum.Procurement, TicketActionEnum.Revise, request?.Note),
            ct);

        return Ok(result);
    }

    [HttpPost("{id:guid}/resubmit")]
    [HttpPut("{id:guid}/resubmit")]
    public async Task<ActionResult<TransitionResult>> Resubmit(
        Guid id,
        [FromBody] ResubmitProcurementTicketRequestDto request,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ResubmitProcurementTicketCommand(id, request),
            ct);

        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<TransitionResult>> Cancel(
        Guid id,
        [FromBody] TicketNoteRequestDto? request,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ExecuteTicketActionCommand(id, TicketTypeEnum.Procurement, TicketActionEnum.Cancel, request?.Note),
            ct);

        return Ok(result);
    }

    [HttpPost("{id:guid}/receive-goods")]
    public async Task<ActionResult<TransitionResult>> ReceiveGoods(
        Guid id,
        [FromBody] TicketNoteRequestDto? request,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new ReceiveProcurementGoodsCommand(id, request?.Note),
            ct);

        return Ok(result);
    }
}
