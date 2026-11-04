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
}