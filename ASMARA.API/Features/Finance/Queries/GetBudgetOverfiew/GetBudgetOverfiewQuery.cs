using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Finance.Queries.GetBudgetOverfiew;

public record GetBudgetOverfiewQuery(int FiscalYear) : IRequest<BudgetOverfiewDto>;

public class GetBudgetOverfiewQueryHandler( IUnitOfWork uow ) : IRequestHandler<GetBudgetOverfiewQuery, BudgetOverfiewDto>
{
    public async Task<BudgetOverfiewDto> Handle(GetBudgetOverfiewQuery request, CancellationToken ct)
    {
        var budgets = await uow.DepartmentBudgets.QueryNoTracking()
            .Where(b => b.FiscalYear == request.FiscalYear)
            .ToListAsync(ct);
        var totalAllocated = budgets.Sum(b => b.AllocatedBudget);
        var totalSpent = budgets.Sum(b => b.SpentBudget);
        var totalCommitted = budgets.Sum(b => b.CommittedBudget);
        var totalRemaining = totalAllocated - totalSpent - totalCommitted;
        var overallUtilization = totalAllocated > 0
            ? Math.Round(((totalSpent + totalCommitted) / totalAllocated) * 100, 1)
            : 0;

        return new BudgetOverfiewDto(
            request.FiscalYear,
            totalAllocated,
            totalSpent,
            totalCommitted,
            totalRemaining,
            overallUtilization
            );
    }
}
