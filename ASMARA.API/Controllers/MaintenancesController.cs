using ASMARA.API.Enums;
using ASMARA.API.Features.Maintenance.Commands.AssignHandlerMaintenanceTicket;
using ASMARA.API.Features.Maintenance.Commands.CreateMaintenanceTicket;
using ASMARA.API.Features.Maintenance.Commands.OrderPart;
using ASMARA.API.Features.Maintenance.Commands.ProcessReplacement;
using ASMARA.API.Features.Maintenance.Commands.ReceiveItem;
using ASMARA.API.Features.Maintenance.Commands.RejectMaintenanceTicket;
using ASMARA.API.Features.Maintenance.Commands.SendReplacement;
using ASMARA.API.Features.Maintenance.Commands.SubmitRepairResult;
using ASMARA.API.Features.Tickets.Commands.ExecuteTicketAction;
using ASMARA.API.Features.Tickets.Queries.GetTicketDetail;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
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

    [HttpPost("{id:guid}/action")]
    public async Task<ActionResult<TransitionResult>> ExecuteAction(
        Guid id,
        [FromBody] TicketApprovalDto request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<TicketActionEnum>(request.Action, ignoreCase: true, out var action) ||
            action is not (TicketActionEnum.Approve or TicketActionEnum.Reject))
        {
            return BadRequest(new { message = $"Aksi '{request.Action}' tidak valid. Aksi yang didukung untuk approval maintenance: Approve, Reject." });
        }

        var result = action switch
        {
            TicketActionEnum.Approve => await sender.Send(
                new ExecuteTicketActionCommand(id, TicketTypeEnum.Maintenance, TicketActionEnum.Approve, request.Note), ct),
            TicketActionEnum.Reject => await sender.Send(
                new RejectMaintenanceTicketCommand(id, request.Note), ct),
            _ => throw new InvalidOperationException()
        };

        return Ok(result);
    }

    [HttpPost("{id:guid}/assign-handler")]
    public async Task<ActionResult<TransitionResult>> AssignHandler(Guid id, [FromBody] AssignTechnicianDto request, CancellationToken ct)
        => Ok(await sender.Send(new AssignHandlerCommand(id, request), ct));

    [HttpPost("{id:guid}/send")]
    public async Task<ActionResult<TransitionResult>> Send(Guid id, [FromBody] TicketNoteRequestDto ? request, CancellationToken ct)
        => Ok(await sender.Send(new ExecuteTicketActionCommand(id, TicketTypeEnum.Maintenance, TicketActionEnum.Send, request?.Note), ct));

    [HttpPost("{id:guid}/receive")]
    public async Task<ActionResult<TransitionResult>> Receive(Guid id, [FromBody] TicketNoteRequestDto? request, CancellationToken ct)
        => Ok(await sender.Send(new ReceiveItemCommand(id, request?.Note), ct));

    [HttpPost("{id:guid}/repair-result")]
    public async Task<ActionResult<TransitionResult>> SubmitRepairResult(Guid id, [FromBody] SubmitRepairResultRequestDto request, CancellationToken ct)
        => Ok(await sender.Send(new SubmitRepairResultCommand(id, request), ct));

    [HttpPost("{id:guid}/part-check")]
    public async Task<ActionResult<TransitionResult>> PartCheck(Guid id, [FromBody] PartCheckRequestDto request, CancellationToken ct)
        => Ok(await sender.Send(new ExecuteTicketActionCommand(id, TicketTypeEnum.Maintenance, request.Available ? TicketActionEnum.PartAvailable : TicketActionEnum.PartUnavailable, request.Note), ct));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<TransitionResult>> Close(Guid id, [FromBody] TicketNoteRequestDto? request, CancellationToken ct)
        => Ok(await sender.Send(new ExecuteTicketActionCommand(id, TicketTypeEnum.Maintenance, TicketActionEnum.Close, request?.Note), ct));

    [HttpPost("{id:guid}/dispose")]
    public async Task<ActionResult<TransitionResult>> Dispose(Guid id, [FromBody] TicketNoteRequestDto? request, CancellationToken ct)
    => Ok(await sender.Send(new ExecuteTicketActionCommand(id, TicketTypeEnum.Maintenance, TicketActionEnum.Dispose, request?.Note), ct));

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<TransitionResult>> Archive(Guid id, [FromBody] TicketNoteRequestDto? request, CancellationToken ct)
        => Ok(await sender.Send(new ExecuteTicketActionCommand(id, TicketTypeEnum.Maintenance, TicketActionEnum.Archive, request?.Note), ct));

    [HttpPost("{id:guid}/replacement")]
    public async Task<ActionResult<TransitionResult>> Replacement(Guid id, [FromBody] ReplacementRequestDto request, CancellationToken ct)
        => Ok(await sender.Send(new ProcessReplacementCommand(id, request), ct));

    [HttpPost("{id:guid}/order-part")]
    public async Task<ActionResult<ChildTicketResult>> OrderPart(Guid id, [FromBody] OrderPartRequestDto request,CancellationToken ct)
        => Ok(await sender.Send(new OrderPartCommand(id, request), ct));

    [HttpPost("{id:guid}/send-replacement")]
    public async Task<ActionResult<TransitionResult>> SendReplacement(Guid id, [FromBody] SendReplacementRequestDto request, CancellationToken ct)
        => Ok(await sender.Send(new SendReplacementCommand(id, request), ct));
}
