
using ASMARA.API.Entities;
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Services.TicketFlow;

public class TicketFlowService
(
    ITicketRepository tickets,
    IWorkflowStepRepository workflowSteps,
    ITicketWorkflowLogRepository logs,
    IApproverResolver approverResolver,
    ICurrentUser currentUser
) : ITicketFlowService
{
    private static readonly HashSet<TicketActionEnum> SystemOnlyActions =
    [
        TicketActionEnum.ChildCompleted,
        TicketActionEnum.ChildRejected,
        TicketActionEnum.OrderPart,
        TicketActionEnum.ReplacementUnavailable
    ];

    public Task<TransitionResult> ExecuteAsync(
        Guid ticketId,
        TicketActionEnum action,
        string? note = null,
        CancellationToken ct = default)
    {
        if (SystemOnlyActions.Contains(action))
        {
            throw new TicketStateException(
                $"{action} is triggered automatically by the system.");
        }

        return ExecuteCoreAsync(
            ticketId,
            action,
            note,
            checkAccess: true,
            ct);
    }

    public async Task<ChildTicketResult> CreateChildAsync(
        Guid parentTicketId,
        TicketActionEnum parentAction,
        Ticket child,
        string? note = null,
        CancellationToken ct = default)
    {
        if (parentAction is not (
            TicketActionEnum.OrderPart or
            TicketActionEnum.ReplacementUnavailable))
        {
            throw new TicketStateException(
                $"Action '{parentAction}' does not create a child ticket.");
        }

        if (child.TicketType != (byte)TicketTypeEnum.Procurement)
        {
            throw new BusinessRuleException(
                "A child ticket must be a Procurement ticket.");
        }

        // Move parent Maintenance to waiting step.
        var parentResult = await ExecuteCoreAsync(
            parentTicketId,
            parentAction,
            note,
            checkAccess: true,
            ct);

        // Prepare child Procurement ticket.
        if (child.Id == Guid.Empty)
            child.Id = Guid.CreateVersion7();

        // Handler yang membuat Procurement menjadi requester child.
        child.EmployeeId = currentUser.EmployeeId;

        // Set parent sebelum workflow child dimulai.
        // Resolver menggunakan ParentTicketId untuk membedakan
        // Procurement dari Maintenance dan Procurement reguler.
        child.ParentTicketId = parentTicketId;

        tickets.Add(child);

        // Start child Procurement workflow.
        var childResult = await ExecuteCoreAsync(
            child.Id,
            TicketActionEnum.Create,
            note,
            checkAccess: true,
            ct,
            allowChildCreate: true);

        return new ChildTicketResult(parentResult, childResult);
    }

    public async Task<TransitionResult> ExecuteCoreAsync(
        Guid ticketId,
        TicketActionEnum action,
        string? note,
        bool checkAccess,
        CancellationToken ct,
        bool allowChildCreate = false)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, ct);

        if (ticket is null)
        {
            throw new NotFoundException(
                $"Ticket with ID {ticketId} not found.");
        }

        if (action == TicketActionEnum.Create &&
            ticket.ParentTicketId is not null &&
            !allowChildCreate)
        {
            throw new TicketStateException(
                "Child tickets can only be created through their parent ticket.");
        }

        var type = (TicketTypeEnum)ticket.TicketType;

        var fromStep = await tickets.GetCurrentStepAsync(ticketId, ct)
            ?? StepCodes.Created;

        var level = await GetStepLevelAsync(
            ticket.TicketType,
            fromStep,
            ct);

        string toStep;

        // Handle child rejection.
        if (action == TicketActionEnum.ChildRejected)
        {
            if (fromStep is not (
                StepCodes.WaitingForPart or
                StepCodes.WaitingForReplacement))
            {
                throw new TicketStateException(
                    $"Parent ticket is not waiting for a child (step '{fromStep}').");
            }

            toStep = await tickets.GetStepBeforeAsync(
                ticketId,
                fromStep,
                ct)
                ?? throw new TicketStateException(
                    $"Cannot find the step before '{fromStep}'.");
        }
        else if (!TransitionMap.TryGetNextStep(
            type,
            fromStep,
            action,
            out toStep))
        {
            throw new TicketStateException(
                $"Action '{action}' is not allowed at step '{fromStep}'.");
        }

        // =========================================
        // TASK ACCESS VALIDATION
        // =========================================

        if (checkAccess &&
            !TaskAccess.CanActOn(
                ticket,
                level,
                currentUser.EmployeeId,
                currentUser.Roles))
        {
            throw new ForbiddenException(
                "You are not allowed to act on this ticket.");
        }

        // =========================================
        // INITIAL APPROVER VALIDATION
        // =========================================

        if (action == TicketActionEnum.Create)
        {
            await approverResolver.ResolveAsync(
                ticket,
                StepLevels.DepartmentManager,
                ct);
        }

        // =========================================
        // NEXT TASK LEVEL
        // =========================================

        var toLevel = await GetStepLevelAsync(
            ticket.TicketType,
            toStep,
            ct);

        var isTerminal = TransitionMap.IsTerminal(type, toStep);

        // =========================================
        // NEXT TASK ASSIGNEE
        // =========================================

        Guid? nextTaskEmployeeId;

        if (isTerminal)
        {
            nextTaskEmployeeId = null;
        }
        else if (
            type == TicketTypeEnum.Maintenance &&
            action == TicketActionEnum.Repaired &&
            toStep == StepCodes.HandlerReturnItem)
        {
            nextTaskEmployeeId = currentUser.EmployeeId;
        }
        else
        {
            nextTaskEmployeeId = await approverResolver.ResolveAsync(
                ticket,
                toLevel,
                ct);
        }

        // =========================================
        // WORKFLOW HISTORY
        // =========================================

        var now = DateTime.UtcNow;

        logs.Add(new TicketWorkflowLog
        {
            TicketId = ticket.Id,
            FromStep = fromStep,
            ToStep = toStep,
            EmployeeId = currentUser.EmployeeId,
            EmployeeName = currentUser.FullName,
            Note = string.IsNullOrWhiteSpace(note)
                ? null
                : note.Trim(),
            CreatedAt = now
        });

        // =========================================
        // UPDATE TICKET
        // =========================================

        ticket.CurrentTaskEmployeeId = nextTaskEmployeeId;
        ticket.UpdatedAt = now;

        if (isTerminal)
            ticket.ClosedAt = now;

        var result = new TransitionResult(
            ticket.Id,
            fromStep,
            toStep,
            isTerminal,
            nextTaskEmployeeId,
            toLevel);

        // =========================================
        // CHILD PROCUREMENT COMPLETION
        // =========================================

        if (isTerminal && ticket.ParentTicketId is Guid parentId)
        {
            var parentAction = toStep == StepCodes.Completed
                ? TicketActionEnum.ChildCompleted
                : TicketActionEnum.ChildRejected;

            await ExecuteCoreAsync(
                parentId,
                parentAction,
                $"Child ticket {ticket.TicketNumber} {toStep.ToLower()}.",
                checkAccess: false,
                ct);
        }

        return result;
    }

    // =========================================
    // WORKFLOW STEP LEVEL RESOLVER
    // =========================================

    private async Task<int?> GetStepLevelAsync(
        byte ticketType,
        string stepCode,
        CancellationToken ct)
    {
        if (stepCode == StepCodes.Created)
            return StepLevels.Requester;

        var step = await workflowSteps.QueryNoTracking()
            .Where(s =>
                s.StepCode == stepCode &&
                s.Workflow.TicketType == ticketType &&
                s.Workflow.IsActive)
            .Select(s => new { s.Level })
            .FirstOrDefaultAsync(ct);

        return step is null
            ? throw new TicketStateException(
                $"Step '{stepCode}' is not defined in the active workflow.")
            : step.Level;
    }
}
