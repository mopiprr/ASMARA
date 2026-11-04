using ASMARA.API.Entities;
using MediatR;

namespace ASMARA.API.Features.Tickets.Events;

public class TicketStatusChangedEvent : INotification
{
    public Ticket Ticket { get; }
    public Employee Requester { get; }
    public Employee Actor { get; }
    public string Action { get; }
    public string? Note { get; }
    public List<ProcurementDetail>? Items { get; }
    public MaintenanceDetail? Maintenance { get; }

    public TicketStatusChangedEvent(
        Ticket ticket,
        Employee requester,
        Employee actor,
        string action,
        string? note,
        List<ProcurementDetail>? items = null,
        MaintenanceDetail? maintenance = null)
    {
        Ticket = ticket;
        Requester = requester;
        Actor = actor;
        Action = action;
        Note = note;
        Items = items;
        Maintenance = maintenance;
    }
}