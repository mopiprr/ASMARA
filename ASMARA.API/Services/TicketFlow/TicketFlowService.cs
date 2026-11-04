using ASMARA.API.Entities;
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Services.TicketFlow;

public class TicketFlowService
    (ITicketRepository tickets, IWorkflowStepRepository workflowSteps, ITicketWorkflowLogRepository logs,
    IApproverResolver approverResolver, ICurrentUser currentUser) 
    : ITicketFlowService
{
    private static readonly HashSet<TicketActionEnum> SystemOnlyActions = [TicketActionEnum.ChildCompleted, TicketActionEnum.ChildRejected];

    public Task<TransitionResult> ExecuteAsync(Guid ticketId, TicketActionEnum action, string? note = null, CancellationToken ct = default)
    {
        if (SystemOnlyActions.Contains(action))
            throw new TicketStateException($"{action} is triggered automatically by the system.");

        return ExecuteCoreAsync(ticketId, action, note, checkAccess: true, ct);
    }

    public async Task<TransitionResult> ExecuteCoreAsync(Guid ticketId, TicketActionEnum action, string? note, bool checkAccess, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, ct);
        if (ticket is null)
        {
            throw new NotFoundException($"Ticket with ID {ticketId} not found.");
        }

        var type = (TicketTypeEnum)ticket.TicketType;

        var fromStep = await tickets.GetCurrentStepAsync(ticketId, ct) ?? StepCodes.Created;

        var level = await GetStepLevelAsync(ticket.TicketType, fromStep, ct);

        string toStep;
        if (action == TicketActionEnum.ChildRejected)
        {
            if (fromStep is not (StepCodes.WaitingForPart or StepCodes.WaitingForReplacement))
                throw new TicketStateException($"Parent ticket is not waiting for a child (step '{fromStep}').");

            toStep = await tickets.GetStepBeforeAsync(ticketId, fromStep, ct)
                ?? throw new TicketStateException($"Cannot find the step before '{fromStep}'.");
        }
        else if (!TransitionMap.TryGetNextStep(type, fromStep, action, out toStep))
        {
            throw new TicketStateException(
                $"Action '{action}' is not allowed at step '{fromStep}'.");
        }

        if (checkAccess && !TaskAccess.CanActOn(ticket, level, currentUser.EmployeeId, currentUser.Roles))
            throw new ForbiddenException("You are not allowed to act on this ticket.");

        if (action == TicketActionEnum.Create)
        {
            var requiredHops = type == TicketTypeEnum.Maintenance
                ? StepLevels.DivisionManager  
                : StepLevels.DepartmentManager;  

            await approverResolver.ResolveAsync(ticket, requiredHops, ct);
        }

        var toLevel = await GetStepLevelAsync(ticket.TicketType, toStep, ct);
        var isTerminal = TransitionMap.IsTerminal(type, toStep);
        var nextTaskEmployeeId = isTerminal
            ? null
            : await approverResolver.ResolveAsync(ticket, toLevel, ct);

        var now = DateTime.UtcNow;
        logs.Add(new TicketWorkflowLog
        {
            TicketId = ticket.Id,
            FromStep = fromStep,
            ToStep = toStep,
            EmployeeId = currentUser.EmployeeId,
            EmployeeName = currentUser.FullName,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = now
        });

        ticket.CurrentTaskEmployeeId = nextTaskEmployeeId;
        ticket.UpdatedAt = now;
        if (isTerminal)
            ticket.ClosedAt = now;

        var result = new TransitionResult(ticket.Id, fromStep, toStep, isTerminal, nextTaskEmployeeId, toLevel);
        if (isTerminal && ticket.ParentTicketId is Guid parentId)
        {
            var parentAction = toStep == StepCodes.Completed
                ? TicketActionEnum.ChildCompleted
                : TicketActionEnum.ChildRejected;

            await ExecuteCoreAsync(parentId, parentAction, $"Child ticket {ticket.TicketNumber} {toStep.ToLower()}.", checkAccess: false, ct);
        }

        return result;
    }

    private async Task<int?> GetStepLevelAsync(byte ticketType, string stepCode, CancellationToken ct)
    {
        if (stepCode == StepCodes.Created)
            return StepLevels.Requester;

        var step = await workflowSteps.QueryNoTracking()
            .Where(s => s.StepCode == stepCode
                     && s.Workflow.TicketType == ticketType
                     && s.Workflow.IsActive)
            .Select(s => new { s.Level })
            .FirstOrDefaultAsync(ct);

        return step is null
            ? throw new TicketStateException($"Step '{stepCode}' is not defined in the active workflow.")
            : step.Level;
    }
}
