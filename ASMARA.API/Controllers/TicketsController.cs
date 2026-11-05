
using ASMARA.API.Common.Paging;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Commands.ExecuteTicketAction;
using ASMARA.API.Features.Tickets.Queries.GetAllMyTicketsQuery;
using ASMARA.API.Features.Tickets.Queries.GetCompletedTickets;
using ASMARA.API.Features.Tickets.Queries.GetPendingTickets;
using ASMARA.API.Features.Tickets.Queries.GetTicketDetail;
using ASMARA.API.Models.Ticket;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketResponseDto>> GetById(
        Guid id,
        CancellationToken ct)
        => Ok(await sender.Send(new GetTicketByIdQuery(id), ct));

    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> GetMyTickets(
        [FromQuery] GetAllMyTicketsQuery query,
        CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    [HttpGet("pending")]
    public async Task<ActionResult<PagedResult<PendingTicketListItemDto>>> GetPendingTickets(
        [FromQuery] GetPendingTicketsQuery query,
        CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    [HttpGet("completed")]
    public async Task<ActionResult<PagedResult<CompletedTicketListItemDto>>> GetCompletedTickets(
        [FromQuery] GetCompletedTicketsQuery query,
        CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    [HttpGet("completed/my")]
    public async Task<ActionResult<PagedResult<CompletedTicketListItemDto>>> GetMyCompletedTickets(
        [FromQuery] GetCompletedTicketsQuery query,
        CancellationToken ct)
        => Ok(await sender.Send(query with { OnlyMyTickets = true }, ct));

    // ==========================================
    // PROCUREMENT APPROVAL ACTION
    // ==========================================

    [HttpPost("{id:guid}/action")]
    public async Task<IActionResult> ExecuteProcurementAction(
        Guid id,
        [FromBody] TicketApprovalDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Action))
        {
            return BadRequest(new
            {
                message = "Action is required."
            });
        }

        var action = request.Action.Trim().ToLowerInvariant() switch
        {
            "approve" => TicketActionEnum.Approve,
            "revise" => TicketActionEnum.Revise,
            "reject" => TicketActionEnum.Reject,
            _ => (TicketActionEnum?)null
        };

        if (action is null)
        {
            return BadRequest(new
            {
                message = "Invalid action. Allowed actions: Approve, Revise, Reject."
            });
        }

        var result = await sender.Send(
            new ExecuteTicketActionCommand(
                id,
                TicketTypeEnum.Procurement,
                action.Value,
                request.Note
            ),
            ct
        );

        return Ok(result);
    }
}
