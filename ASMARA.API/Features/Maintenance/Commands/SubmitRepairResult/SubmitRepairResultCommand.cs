using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.SubmitRepairResult;

public record SubmitRepairResultCommand(Guid TicketId, SubmitRepairResultRequestDto Request) : IRequest<TransitionResult>;

public class SubmitRepairResultCommandHandler(IUnitOfWork uow, ITicketFlowService ticketFlow, ICurrentUser currentUser, IMediator mediator)
    : IRequestHandler<SubmitRepairResultCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(SubmitRepairResultCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var ticket = await uow.Tickets.Query()
            .Include(t => t.Employee)
            .Include(t => t.MaintenanceDetail)
            .FirstOrDefaultAsync(t => t.Id == command.TicketId
                                   && t.TicketType == (byte)TicketTypeEnum.Maintenance, ct);
        if (ticket == null)
            throw new NotFoundException($"Maintenance ticket {command.TicketId} was not found.");

        var detail = ticket.MaintenanceDetail!;
        var handlerNote = string.IsNullOrWhiteSpace(request.HandlerNote) ? null : request.HandlerNote.Trim();

        TicketActionEnum action;
        switch (request.Result)
        {
            case RepairResultEnum.Repaired:
                action = TicketActionEnum.Repaired;
                detail.RepairResult = (byte)RepairResultEnum.Repaired;
                detail.HandlerNote = handlerNote;
                break;

            case RepairResultEnum.Unrepairable:
                action = TicketActionEnum.Unrepairable;
                detail.RepairResult = (byte)RepairResultEnum.Unrepairable;
                detail.HandlerNote = handlerNote;
                break;

            default: 
                action = TicketActionEnum.NeedPart;
                detail.PartRequirement = request.PartRequirement!.Trim();
                if (handlerNote is not null) detail.HandlerNote = handlerNote;
                break;
        }

        var result = await ticketFlow.ExecuteAsync(ticket.Id, action, note: null, ct);

        await uow.SaveChangesAsync(ct);

        var actor = await uow.Employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;
        await mediator.Publish(new TicketStatusChangedEvent(
            ticket, 
            ticket.Employee, 
            actor, action.ToString(), 
            handlerNote), ct);

        return result;
    }
}