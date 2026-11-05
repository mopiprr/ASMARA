using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.AssignHandlerMaintenanceTicket;

public record AssignHandlerCommand(Guid TicketId, AssignTechnicianDto Request) : IRequest<TransitionResult>;

public class AssignHandlerCommandHandler(IUnitOfWork uow, ITicketRepository tickets, IEmployeeRepository employees,
    ITicketFlowService ticketFlow, ICurrentUser currentUser, IMediator mediator)
    : IRequestHandler<AssignHandlerCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(AssignHandlerCommand command, CancellationToken ct)
    {
        var ticket = await tickets.Query()
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == (byte)TicketTypeEnum.Maintenance, ct);
        if (ticket == null)
            throw new NotFoundException($"Maintenance ticket {command.TicketId} was not found.");

        var handler = await employees.GetActiveHandlers()
            .Where(e => e.Id == command.Request.TechnicianId)
            .Select(e => new { e.Id, e.FullName })
            .FirstOrDefaultAsync(ct);
        if (handler == null)
            throw new BusinessRuleException("Selected employee is not an active Maintenance Handler.");

        ticket.AssignedToEmployeeId = handler.Id;
        ticket.AssignedTo = handler.FullName; 

        var result = await ticketFlow.ExecuteAsync(ticket.Id, TicketActionEnum.AssignHandler, command.Request.InstructionNote, ct);

        await uow.SaveChangesAsync(ct);

        var actor = await employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;
        await mediator.Publish(new TicketStatusChangedEvent(
            ticket, 
            ticket.Employee, 
            actor, 
            TicketActionEnum.AssignHandler.ToString(),
            command.Request.InstructionNote), ct);

        return result;
    }
}