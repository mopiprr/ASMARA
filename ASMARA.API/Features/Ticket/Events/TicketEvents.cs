using Asmara.API.Entities;
using MediatR;

namespace ASMARA.API.Features.Ticket.Events;

public record TicketCreatedEvent(
    Asmara.API.Entities.Ticket Ticket,
    Employee Requester, 
    Employee? DirectManager, 
    IEnumerable<ProcurementDetail>? ProcurementItems = null,
    MaintenanceDetail? MaintenanceDetail = null) : INotification;
    
    public record TicketManagerApprovedEvent(
        Asmara.API.Entities.Ticket Ticket,
        Employee Requester,
        Employee Manager,
        string? Note = null ) : INotification;