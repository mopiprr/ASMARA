using ASMARA.API.Entities;

using MediatR;

namespace ASMARA.API.Features.Tickets.Events;

public record TicketCreatedEvent(
    Ticket Ticket,
    Employee Requester, 
    Employee? DirectManager, 
    IEnumerable<ProcurementDetail>? ProcurementItems = null,
    MaintenanceDetail? MaintenanceDetail = null) : INotification;
    
    public record TicketManagerApprovedEvent(
        Ticket Ticket,
        Employee Requester,
        Employee Manager,
        string? Note = null ) : INotification;