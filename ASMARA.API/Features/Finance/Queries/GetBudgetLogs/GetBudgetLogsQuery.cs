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
                .Include(l => l.RelatedDepartment)
                .Include(l => l.CreatedByEmployee)
                .AsQueryable();

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(l => l.DepartmentBudget.DepartmentId == request.DepartmentId.Value);
        }

        if (request.TicketId.HasValue)
        {
            query = query.Where(l => l.TicketId == request.TicketId.Value);
        }

        var projected = query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new BudgetLogDto(
                l.Id,
                l.LogNumber,
                l.DepartmentBudgetId,
                l.DepartmentBudget.Department.DepartmentName,
                l.TicketId,
                l.Ticket != null ? l.Ticket.TicketNumber : null,
                l.RelatedDepartmentId,
                l.RelatedDepartment != null ? l.RelatedDepartment.DepartmentName : null,
                l.ChangeType,
                ((BudgetChangeTypeEnum)l.ChangeType).ToString(),
                l.Amount,
                l.PreviousAllocated,
                l.NewAllocated,
                l.PreviousRemaining,
                l.NewRemaining,
                l.Description,
                l.CreatedByEmployee.FullName,
                l.CreatedAt
            ));

        return await projected.ToPagedResultAsync(request.Page, request.PageSize, ct: ct);
    }
}
