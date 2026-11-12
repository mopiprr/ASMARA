using ASMARA.API.Common.Paging;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Finance.Queries.GetBudgetLogs;

public record GetBudgetLogsQuery(
        Guid? DepartmentId = null,
        Guid? TicketId = null,
        int Page = 1,
        int PageSize = 10
    ) : IRequest<PagedResult<BudgetLogDto>>;

public class GetBudgetLogsQueryHandler(IUnitOfWork uow) : IRequestHandler<GetBudgetLogsQuery, PagedResult<BudgetLogDto>>
{
    public async Task<PagedResult<BudgetLogDto>> Handle(GetBudgetLogsQuery request, CancellationToken ct)
    {
        var query = uow.BudgetLogs.QueryNoTracking()
            .Include(l => l.DepartmentBudget)
                .ThenInclude(db => db.Department)
            .Include(l => l.Ticket)
            .AsQueryable();

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(l => l.DepartmentBudget!.DepartmentId == request.DepartmentId.Value);
        }

        if (request.TicketId.HasValue)
        {
            query = query.Where(l => l.TicketId == request.TicketId.Value);
        }

        var employees = uow.Employees.QueryNoTracking();
        var departments = uow.Departments.QueryNoTracking();

        var projected = from l in query
                        join emp in employees on l.CreatedByEmployeeId equals emp.Id into empGroup
                        from emp in empGroup.DefaultIfEmpty()
                        join relDept in departments on l.RelatedDepartmentId equals relDept.Id into deptGroup
                        from relDept in deptGroup.DefaultIfEmpty()
                        orderby l.CreatedAt descending
                        select new BudgetLogDto(
                            l.Id,
                            l.LogNumber,
                            l.DepartmentBudgetId,
                            l.DepartmentBudget!.Department!.DepartmentName,
                            l.TicketId,
                            l.Ticket != null ? l.Ticket!.TicketNumber : null,
                            l.RelatedDepartmentId,
                            relDept != null ? relDept.DepartmentName : null,
                            l.ChangeType,
                            ((BudgetChangeTypeEnum)l.ChangeType).ToString(),
                            l.Amount,
                            l.PreviousAllocated,
                            l.NewAllocated,
                            l.PreviousRemaining,
                            l.NewRemaining,
                            l.Description,
                            emp != null ? emp.FullName : "System",
                            l.CreatedAt
                        );

        return await projected.ToPagedResultAsync(request.Page, request.PageSize, ct: ct);
    }
}
