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
    public async Task<TransitionResult> ExecuteAsync(Guid ticketId, TicketActionEnum action, string? note = null, CancellationToken ct = default)
    {
        var ticket = await tickets.GetByIdAsync(ticketId);
        if (ticket is null)
        {
            throw new NotFoundException($"Ticket with ID {ticketId} not found.");
        }

        var type = (TicketTypeEnum)ticket.TicketType;

        var fromStep = await tickets.GetCurrentStepAsync(ticketId, ct) ?? StepCodes.Created;

        var level = await GetStepLevelAsync(ticket.TicketType, fromStep, ct);

        if (!TransitionMap.TryGetNextStep(type, fromStep, action, out var toStep))
            throw new TicketStateException(
                $"Action '{action}' is not allowed at step '{fromStep}'.");

        if (!TaskAccess.CanActOn(ticket, level, currentUser.EmployeeId, currentUser.Roles))
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

        return new TransitionResult(ticket.Id, fromStep, toStep, isTerminal, nextTaskEmployeeId, toLevel);
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
