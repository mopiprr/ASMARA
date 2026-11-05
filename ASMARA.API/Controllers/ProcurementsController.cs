
using ASMARA.API.Features.Procurement.Commands.CreateProcurementTicket;
using ASMARA.API.Features.Procurement.Queries.GetProcurementTickets;
using ASMARA.API.Features.Tickets.Queries.GetTicketDetail;
using ASMARA.API.Models.Ticket;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Controllers;

[ApiController]
[Route("api/procurements")]
[Authorize]
public class ProcurementsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TicketResponseDto>>> GetAll(
        CancellationToken ct)
    {
        var result = await sender.Send(
            new GetProcurementTicketsQuery(), ct);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TicketResponseDto>> Create(
        // [FromBody] CreateTicketProcurementDto request,
        [FromBody] CreateProcurementTicketRequestDto request,
        CancellationToken ct)
    {
        var id = await sender.Send(
            new CreateProcurementTicketCommand(request), ct);

        var ticket = await sender.Send(
            new GetTicketByIdQuery(id), ct);

        return CreatedAtAction(
            nameof(TicketsController.GetById),
            "Tickets",
            new { id },
            ticket);
    }
}
