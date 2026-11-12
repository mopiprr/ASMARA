
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Commands.ExecuteTicketAction;

public record ExecuteTicketActionCommand(
    Guid TicketId,
    TicketTypeEnum ExpectedType,
    TicketActionEnum Action,
    string? Note
) : IRequest<TransitionResult>;

public class ExecuteTicketActionCommandHandler(
    ITicketFlowService ticketFlow,
    IUnitOfWork uow,
    IMediator mediator,
    ICurrentUser currentUser
) : IRequestHandler<ExecuteTicketActionCommand, TransitionResult>
{
    private static readonly HashSet<TicketActionEnum> SimpleActions =
    [
        TicketActionEnum.Approve,
        TicketActionEnum.Revise,
        TicketActionEnum.Reject,
        TicketActionEnum.Resubmit,
        TicketActionEnum.Cancel,
        TicketActionEnum.Send,
        TicketActionEnum.Dispose,
        TicketActionEnum.Archive,
        TicketActionEnum.PartAvailable,
        TicketActionEnum.PartUnavailable,
        TicketActionEnum.Close
    ];

    public async Task<TransitionResult> Handle(
        ExecuteTicketActionCommand command,
        CancellationToken ct)
    {
        // =========================================
        // VALIDATE ACTION
        // =========================================

        var isMaintenanceReject =
            command.Action == TicketActionEnum.Reject &&
            command.ExpectedType == TicketTypeEnum.Maintenance;

        if (!SimpleActions.Contains(command.Action) || isMaintenanceReject)
        {
            throw new InvalidOperationException(
                $"{command.Action} ({command.ExpectedType}) has side effects; use its dedicated command.");
        }

        // =========================================
        // GET TICKET
        // =========================================

        var type = (byte)command.ExpectedType;

        var ticket = await uow.Tickets.Query()
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(
                t => t.Id == command.TicketId &&
                     t.TicketType == type,
                ct);

        if (ticket is null)
        {
            throw new NotFoundException(
                $"{command.ExpectedType} ticket {command.TicketId} was not found.");
        }

        // =========================================
        // VALIDATE CURRENT STEP
        // =========================================

        var currentStep = await uow.Tickets.GetCurrentStepAsync(
            command.TicketId,
            ct);

        // Inventory must use the dedicated command
        // to select the replacement asset.
        if (
            command.ExpectedType == TicketTypeEnum.Maintenance &&
            command.Action == TicketActionEnum.Send &&
            currentStep == StepCodes.InventorySendReplacement)
        {
            throw new BusinessRuleException(
                "Use send-replacement to choose the replacement asset.");
        }

        // =========================================
        // EXECUTE WORKFLOW TRANSITION
        // =========================================

        var result = await ticketFlow.ExecuteAsync(
            command.TicketId,
            command.Action,
            command.Note,
            ct);

        // =========================================
        // SAVE CHANGES
        // =========================================

        await uow.SaveChangesAsync(ct);

        // =========================================
        // PUBLISH TICKET STATUS EVENT
        // =========================================

        var actor = await uow.Employees.GetByIdAsync(
            currentUser.EmployeeId,
            ct) ?? ticket.Employee;

        await mediator.Publish(
            new TicketStatusChangedEvent(
                ticket,
                ticket.Employee,
                actor,
                command.Action.ToString(),
                command.Note),
            ct);

        return result;
    }
}
