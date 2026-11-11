using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Finance.Queries.CheckBudget;

public record CheckBudgetQuery(string DepartmentName, decimal EstimatedTotal) : IRequest<BudgetCheckResultDto>;

public class CheckBudgetQueryHandler(IUnitOfWork uow) : IRequestHandler<CheckBudgetQuery, BudgetCheckResultDto>
{
    public async Task<BudgetCheckResultDto> Handle(CheckBudgetQuery request, CancellationToken ct)
    {
        var currentYear = DateTime.UtcNow.Year;
        var deptName = request.DepartmentName.Trim();

        var budget = await uow.DepartmentBudgets.QueryNoTracking()
            .Include(b => b.Department)
            .FirstOrDefaultAsync(b =>
                b.FiscalYear == currentYear &&
                (b.Department.DepartmentName == deptName || b.Department.DepartmentCode == deptName), ct);

        if (budget == null)
        {
            throw new NotFoundException($"Active budget for department '{deptName}' in fiscal year {currentYear} was not found");
        }

        var remaining = budget.AllocatedBudget - budget.SpentBudget - budget.CommittedBudget;
        var isSufficient = remaining >= request.EstimatedTotal;
        var projectedUsed = budget.SpentBudget + budget.CommittedBudget + request.EstimatedTotal;
        var projectedUtilization = budget.AllocatedBudget > 0
            ? Math.Round((projectedUsed / budget.AllocatedBudget) * 100, 1)
            : 0;

        var status = !isSufficient
            ? "exceeded"
            : (remaining - request.EstimatedTotal <= (budget.AllocatedBudget * 0.10m))
                ? "warning"
                : "normal";

        var message = !isSufficient
            ? $"Estimated cost (IDR {request.EstimatedTotal:N0}) exceeds the available department budget of IDR {remaining:N0}."
            : status == "warning"
                ? "Budget is sufficient, but this expenditure will reduce remaining funds below 10%."
                : "Department budget is sufficient for this procurement request.";

        return new BudgetCheckResultDto(
            budget.Department.DepartmentName,
            request.EstimatedTotal,
            remaining,
            isSufficient,
            projectedUtilization,
            status,
            message
            );
    }
}
