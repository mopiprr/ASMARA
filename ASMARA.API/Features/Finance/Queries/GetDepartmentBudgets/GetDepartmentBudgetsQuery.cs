using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Finance.Queries.GetDepartmentBudgets;

public record GetDepartmentBudgetsQuery(int FiscalYear) : IRequest<List<DepartmentBudgetDto>>;
public class GetDepartmentBudgetsQueryHandler(IUnitOfWork uow) : IRequestHandler<GetDepartmentBudgetsQuery, List<DepartmentBudgetDto>>
{
    public async Task<List<DepartmentBudgetDto>> Handle(GetDepartmentBudgetsQuery request, CancellationToken ct)
    {
        var budgets = await uow.DepartmentBudgets.QueryNoTracking()
            .Include(b => b.Department)
            .Where(b => b.FiscalYear == request.FiscalYear)
            .OrderBy(b => b.Department.DepartmentName)
            .ToListAsync(ct);

        return budgets.Select(b =>
        {
            var remaining = b.AllocatedBudget - b.SpentBudget - b.CommittedBudget;
            var utilization = b.AllocatedBudget > 0
                    ? Math.Round(((b.SpentBudget + b.CommittedBudget) / b.AllocatedBudget) * 100, 1)
                    : 0;

            var status = remaining < 0
                   ? "exceeded"
                   : (b.AllocatedBudget > 0 && remaining <= (b.AllocatedBudget * 0.10m))
                       ? "critical"
                       : (b.AllocatedBudget > 0 && remaining <= (b.AllocatedBudget * 0.20m))
                           ? "warning"
                           : "normal";

            return new DepartmentBudgetDto(
                    b.Id,
                    b.DepartmentId,
                    b.Department.DepartmentCode,
                    b.Department.DepartmentName,
                    b.FiscalYear,
                    b.AllocatedBudget,
                    b.SpentBudget,
                    b.CommittedBudget,
                    remaining,
                    utilization,
                    status,
                    b.CreatedAt,
                    b.UpdatedAt
                );
        }).ToList();
    }
}
