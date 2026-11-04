using ASMARA.API.Entities;
using MediatR;

namespace ASMARA.API.Features.Tickets.Events;

public record TicketCreatedEvent(
    ASMARA.API.Entities.Ticket Ticket,
    Employee Requester,
    Employee? DirectManager,
    IEnumerable<ProcurementDetail>? ProcurementItems = null,
    MaintenanceDetail? MaintenanceDetail = null
) : INotification;
