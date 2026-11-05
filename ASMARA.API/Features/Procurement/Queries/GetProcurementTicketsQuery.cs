
using ASMARA.API.Models.Ticket;
using MediatR;

namespace ASMARA.API.Features.Procurement.Queries.GetProcurementTickets;

public record GetProcurementTicketsQuery
    : IRequest<List<TicketResponseDto>>;
