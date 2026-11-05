using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Commands.ExecuteTicketAction;

public record ExecuteTicketActionCommand(Guid TicketId, TicketTypeEnum ExpectedType, TicketActionEnum Action, string? Note) 
    : IRequest<TransitionResult>;

public class ExecuteTicketActionCommandHandler(ITicketRepository tickets, ITicketFlowService ticketFlow, IUnitOfWork uow) 
    : IRequestHandler<ExecuteTicketActionCommand, TransitionResult>
{
    // Aksi yang aman lewat jalur ini (tidak mengubah data lain selain step)
    private static readonly HashSet<TicketActionEnum> SimpleActions =
    [
        TicketActionEnum.Approve, TicketActionEnum.Revise, TicketActionEnum.Reject,
        TicketActionEnum.Resubmit, TicketActionEnum.Cancel, TicketActionEnum.Send,
        TicketActionEnum.Dispose, TicketActionEnum.Archive,
        TicketActionEnum.PartAvailable, TicketActionEnum.PartUnavailable, TicketActionEnum.Close
    ];

    public async Task<TransitionResult> Handle(ExecuteTicketActionCommand command, CancellationToken ct)
    {
        var isMaintenanceReject = command.Action == TicketActionEnum.Reject && command.ExpectedType == TicketTypeEnum.Maintenance;
        if (!SimpleActions.Contains(command.Action) || isMaintenanceReject)
            throw new InvalidOperationException($"{command.Action} ({command.ExpectedType}) has side effects; use its dedicated command.");

        var type = (byte)command.ExpectedType;
        if (!await tickets.QueryNoTracking().AnyAsync(t => t.Id == command.TicketId && t.TicketType == type, ct))
            throw new NotFoundException($"{command.ExpectedType} ticket {command.TicketId} was not found.");

        var result = await ticketFlow.ExecuteAsync(command.TicketId, command.Action, command.Note, ct);

        await uow.SaveChangesAsync(ct);

        return result;
    }
}