using ASMARA.API.Common.Paging;
using ASMARA.API.Features.Procurement.Queries.GetPurchaseProof;
using ASMARA.API.Features.Tickets.Queries.GetAllMyTicketsQuery;
using ASMARA.API.Features.Tickets.Queries.GetMyTicketStatistics;
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
    public async Task<ActionResult<TicketResponseDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetTicketByIdQuery(id), ct));

    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> GetMyTickets([FromQuery] GetAllMyTicketsQuery query, CancellationToken ct)
       => Ok(await sender.Send(query, ct));

    [HttpGet("pending")]
    public async Task<ActionResult<PagedResult<PendingTicketListItemDto>>> GetPendingTickets([FromQuery] GetPendingTicketsQuery query, CancellationToken ct)
       => Ok(await sender.Send(query, ct));

    [HttpGet("pending/{id:guid}")]
    public async Task<ActionResult<TicketResponseDto>> GetPendingTicketById(Guid id, CancellationToken ct)
       => Ok(await sender.Send(new GetPendingTicketByIdQuery(id), ct));

    [HttpGet("my-statistics")]
    public async Task<ActionResult<MyTicketStatisticsDto>> GetMyStatistics([FromQuery] GetMyTicketStatisticsQuery query, CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    [HttpGet("{id:guid}/purchase-proof")]
    public async Task<IActionResult> GetPurchaseProof(Guid id, CancellationToken ct)
    {
        await sender.Send(new GetTicketByIdQuery(id), ct);

        var proof = await sender.Send(new GetPurchaseProofQuery(id), ct);

        Response.Headers["X-Content-Type-Options"] = "nosniff";

        var openInline = (proof.ContentType.StartsWith("image/") && proof.ContentType != "image/svg+xml")
                         || proof.ContentType == "application/pdf";

        return openInline
            ? File(proof.Content, proof.ContentType)
            : File(proof.Content, proof.ContentType, proof.FileName);
    }
}