using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Commands.ExecuteTicketAction;

public record ExecuteTicketActionCommand(Guid TicketId, TicketTypeEnum ExpectedType, TicketActionEnum Action, string? Note) 
    : IRequest<TransitionResult>;
 
public class ExecuteTicketActionCommandHandler(
    ITicketFlowService ticketFlow,
    IUnitOfWork uow,
    IMediator mediator,
    ICurrentUser currentUser) 
    : IRequestHandler<ExecuteTicketActionCommand, TransitionResult>
{
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
        var ticket = await uow.Tickets.Query()
            .Include(t => t.Employee)
                .ThenInclude(e => e.Department)
            .Include(t => t.ProcurementDetails)
            .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == type, ct);

        if (ticket is null)
            throw new NotFoundException($"{command.ExpectedType} ticket {command.TicketId} was not found.");

        // Jika ini tiket pengadaan dan Finance meng-approve: periksa dan ikat budget departemen pemohon
        if (command.ExpectedType == TicketTypeEnum.Procurement && command.Action == TicketActionEnum.Approve)
        {
            var currentStep = await uow.Tickets.GetCurrentStepAsync(ticket.Id, ct);
            if (currentStep == StepCodes.FinanceApproval)
            {
                var ticketTotal = ticket.ProcurementDetails.Sum(p => p.Quantity * p.Price);
                var currentYear = DateTime.UtcNow.Year;
                var deptBudget = await uow.DepartmentBudgets.Query()
                    .FirstOrDefaultAsync(b => b.DepartmentId == ticket.Employee.DepartmentId && b.FiscalYear == currentYear, ct);

                if (deptBudget != null)
                {
                    var remaining = deptBudget.AllocatedBudget - deptBudget.SpentBudget - deptBudget.CommittedBudget;
                    if (remaining < ticketTotal)
                    {
                        throw new BusinessRuleException(
                            $"Cannot approve procurement ticket: Department budget for '{ticket.Employee.Department?.DepartmentName}' has insufficient balance. Available: IDR {remaining:N0}, Required: IDR {ticketTotal:N0}.");
                    }

                    deptBudget.CommittedBudget += ticketTotal;
                    deptBudget.UpdatedAt = DateTime.UtcNow;

                    var logCount = await uow.BudgetLogs.QueryNoTracking().CountAsync(ct) + 1;
                    uow.BudgetLogs.Add(new BudgetLog
                    {
                        DepartmentBudgetId = deptBudget.Id,
                        TicketId = ticket.Id,
                        LogNumber = $"LOG-{DateTime.UtcNow:yyyyMM}-{logCount:D4}",
                        ChangeType = (byte)BudgetChangeTypeEnum.TicketCommitment,
                        Amount = ticketTotal,
                        PreviousAllocated = deptBudget.AllocatedBudget,
                        NewAllocated = deptBudget.AllocatedBudget,
                        PreviousRemaining = remaining,
                        NewRemaining = remaining - ticketTotal,
                        Description = $"Funds committed for Procurement Ticket #{ticket.TicketNumber} ({ticket.Subject})",
                        CreatedByEmployeeId = currentUser.EmployeeId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        var result = await ticketFlow.ExecuteAsync(command.TicketId, command.Action, command.Note, ct);

        if (result.FromStep == StepCodes.InventorySendReplacement)
            throw new BusinessRuleException("Use send-replacement to choose the replacement asset.");

        // Jika tiket pengadaan ditolak setelah sebelumnya sempat disetujui (komitmen aktif): lepas komitmen
        if (command.ExpectedType == TicketTypeEnum.Procurement && command.Action == TicketActionEnum.Reject)
        {
            var ticketTotal = ticket.ProcurementDetails.Sum(p => p.Quantity * p.Price);
            var hadCommitment = await uow.BudgetLogs.QueryNoTracking()
                .AnyAsync(l => l.TicketId == ticket.Id && l.ChangeType == (byte)BudgetChangeTypeEnum.TicketCommitment, ct);
            var wasReleased = await uow.BudgetLogs.QueryNoTracking()
                .AnyAsync(l => l.TicketId == ticket.Id && l.ChangeType == (byte)BudgetChangeTypeEnum.TicketRelease, ct);

            if (hadCommitment && !wasReleased)
            {
                var currentYear = DateTime.UtcNow.Year;
                var deptBudget = await uow.DepartmentBudgets.Query()
                    .FirstOrDefaultAsync(b => b.DepartmentId == ticket.Employee.DepartmentId && b.FiscalYear == currentYear, ct);

                if (deptBudget != null)
                {
                    var prevRemaining = deptBudget.AllocatedBudget - deptBudget.SpentBudget - deptBudget.CommittedBudget;
                    deptBudget.CommittedBudget = Math.Max(0, deptBudget.CommittedBudget - ticketTotal);
                    deptBudget.UpdatedAt = DateTime.UtcNow;

                    var newRemaining = deptBudget.AllocatedBudget - deptBudget.SpentBudget - deptBudget.CommittedBudget;
                    var logCount = await uow.BudgetLogs.QueryNoTracking().CountAsync(ct) + 1;
                    uow.BudgetLogs.Add(new BudgetLog
                    {
                        DepartmentBudgetId = deptBudget.Id,
                        TicketId = ticket.Id,
                        LogNumber = $"LOG-{DateTime.UtcNow:yyyyMM}-{logCount:D4}",
                        ChangeType = (byte)BudgetChangeTypeEnum.TicketRelease,
                        Amount = ticketTotal,
                        PreviousAllocated = deptBudget.AllocatedBudget,
                        NewAllocated = deptBudget.AllocatedBudget,
                        PreviousRemaining = prevRemaining,
                        NewRemaining = newRemaining,
                        Description = $"Commitment released for rejected Procurement Ticket #{ticket.TicketNumber}",
                        CreatedByEmployeeId = currentUser.EmployeeId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await uow.SaveChangesAsync(ct);

        var actor = await uow.Employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;

        await mediator.Publish(new TicketStatusChangedEvent(
            ticket,
            ticket.Employee,
            actor,
            command.Action.ToString(),
            command.Note), ct);

        return result;
    }
}