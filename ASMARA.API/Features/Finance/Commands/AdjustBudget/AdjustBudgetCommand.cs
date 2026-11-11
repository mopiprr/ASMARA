using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Finance.Commands.AdjustBudget;

public record AdjustBudgetCommand(AdjustBudgetRequestDto Request) : IRequest<DepartmentBudgetDto>;

public class AdjustBudgetCommandHandler(
    IUnitOfWork uow,
    ICurrentUser currentUser
) : IRequestHandler<AdjustBudgetCommand, DepartmentBudgetDto>
{
    public async Task<DepartmentBudgetDto> Handle(AdjustBudgetCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var currentYear = DateTime.UtcNow.Year;

        var budget = await uow.DepartmentBudgets.Query()
            .Include(b => b.Department)
            .FirstOrDefaultAsync(b => b.DepartmentId == req.DepartmentId && b.FiscalYear == currentYear, ct);

        if (budget == null)
            throw new NotFoundException($"Active budget for department {req.DepartmentId} in year {currentYear} was not found.");

        var changeType = (BudgetChangeTypeEnum)req.ChangeType;
        var prevAllocated = budget.AllocatedBudget;
        var prevRemaining = budget.AllocatedBudget - budget.SpentBudget - budget.CommittedBudget;

        return await uow.ExecuteInTransactionAsync(async txCt =>
        {
            var logCount = await uow.BudgetLogs.QueryNoTracking().CountAsync(txCt) + 1;
            var logNumber = $"LOG-{DateTime.UtcNow:yyyyMM}-{logCount:D4}";

            if (changeType == BudgetChangeTypeEnum.AllocationIncrease)
            {
                budget.AllocatedBudget += req.Amount;
                budget.UpdatedAt = DateTime.UtcNow;

                var newRemaining = budget.AllocatedBudget - budget.SpentBudget - budget.CommittedBudget;

                uow.BudgetLogs.Add(new BudgetLog
                {
                    DepartmentBudgetId = budget.Id,
                    LogNumber = logNumber,
                    ChangeType = (byte)changeType,
                    Amount = req.Amount,
                    PreviousAllocated = prevAllocated,
                    NewAllocated = budget.AllocatedBudget,
                    PreviousRemaining = prevRemaining,
                    NewRemaining = newRemaining,
                    Description = req.Reason,
                    CreatedByEmployeeId = currentUser.EmployeeId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (changeType == BudgetChangeTypeEnum.AllocationDecrease)
            {
                if (prevRemaining < req.Amount)
                {
                    throw new BusinessRuleException($"Cannot decrease allocation by IDR {req.Amount:N0} because remaining budget is only IDR {prevRemaining:N0}.");
                }
                    


                budget.AllocatedBudget -= req.Amount;
                budget.UpdatedAt = DateTime.UtcNow;

                var newRemaining = budget.AllocatedBudget - budget.SpentBudget - budget.CommittedBudget;

                uow.BudgetLogs.Add(new BudgetLog
                {
                    DepartmentBudgetId = budget.Id,
                    LogNumber = logNumber,
                    ChangeType = (byte)changeType,
                    Amount = req.Amount,
                    PreviousAllocated = prevAllocated,
                    NewAllocated = budget.AllocatedBudget,
                    PreviousRemaining = prevRemaining,
                    NewRemaining = newRemaining,
                    Description = req.Reason,
                    CreatedByEmployeeId = currentUser.EmployeeId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (changeType == BudgetChangeTypeEnum.TransferOut || changeType == BudgetChangeTypeEnum.TransferIn)
            {
                if (!req.TargetDepartmentId.HasValue)
                    throw new BusinessRuleException("Target department is required for budget transfers.");

                if (prevRemaining < req.Amount)
                    throw new BusinessRuleException($"Source department has insufficient remaining budget (IDR {prevRemaining:N0}) for this transfer.");

                var targetBudget = await uow.DepartmentBudgets.Query()
                    .Include(b => b.Department)
                    .FirstOrDefaultAsync(b => b.DepartmentId == req.TargetDepartmentId.Value && b.FiscalYear == currentYear, txCt);

                if (targetBudget == null)
                    throw new NotFoundException($"Target department budget for year {currentYear} was not found.");

                // 1. Potong dari departemen asal (Transfer Out)                                                                                        
                budget.AllocatedBudget -= req.Amount;
                budget.UpdatedAt = DateTime.UtcNow;
                var sourceNewRemaining = budget.AllocatedBudget - budget.SpentBudget - budget.CommittedBudget;

                uow.BudgetLogs.Add(new BudgetLog
                {
                    DepartmentBudgetId = budget.Id,
                    RelatedDepartmentId = req.TargetDepartmentId,
                    LogNumber = logNumber,
                    ChangeType = (byte)BudgetChangeTypeEnum.TransferOut,
                    Amount = req.Amount,
                    PreviousAllocated = prevAllocated,
                    NewAllocated = budget.AllocatedBudget,
                    PreviousRemaining = prevRemaining,
                    NewRemaining = sourceNewRemaining,
                    Description = $"Budget transfer to {targetBudget.Department.DepartmentName}: {req.Reason}",
                    CreatedByEmployeeId = currentUser.EmployeeId,
                    CreatedAt = DateTime.UtcNow
                });

                // 2. Tambahkan ke departemen tujuan (Transfer In)                                                                                      
                var targetPrevAllocated = targetBudget.AllocatedBudget;
                var targetPrevRemaining = targetBudget.AllocatedBudget - targetBudget.SpentBudget - targetBudget.CommittedBudget;
                targetBudget.AllocatedBudget += req.Amount;
                targetBudget.UpdatedAt = DateTime.UtcNow;
                var targetNewRemaining = targetBudget.AllocatedBudget - targetBudget.SpentBudget - targetBudget.CommittedBudget;

                var logNumberIn = $"LOG-{DateTime.UtcNow:yyyyMM}-{(logCount + 1):D4}";
                uow.BudgetLogs.Add(new BudgetLog
                {
                    DepartmentBudgetId = targetBudget.Id,
                    RelatedDepartmentId = budget.DepartmentId,
                    LogNumber = logNumberIn,
                    ChangeType = (byte)BudgetChangeTypeEnum.TransferIn,
                    Amount = req.Amount,
                    PreviousAllocated = targetPrevAllocated,
                    NewAllocated = targetBudget.AllocatedBudget,
                    PreviousRemaining = targetPrevRemaining,
                    NewRemaining = targetNewRemaining,
                    Description = $"Budget transfer from {budget.Department.DepartmentName}: {req.Reason}",
                    CreatedByEmployeeId = currentUser.EmployeeId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await uow.SaveChangesAsync(txCt);

            var finalRemaining = budget.AllocatedBudget - budget.SpentBudget - budget.CommittedBudget;
            var finalUtilization = budget.AllocatedBudget > 0
                ? Math.Round(((budget.SpentBudget + budget.CommittedBudget) / budget.AllocatedBudget) * 100, 1)
                : 0;

            return new DepartmentBudgetDto(
                budget.Id,
                budget.DepartmentId,
                budget.Department.DepartmentCode,
                budget.Department.DepartmentName,
                budget.FiscalYear,
                budget.AllocatedBudget,
                budget.SpentBudget,
                budget.CommittedBudget,
                finalRemaining,
                finalUtilization,
                "normal",
                budget.CreatedAt,
                budget.UpdatedAt
            );
        }, ct);
    }
}