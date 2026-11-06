using ASMARA.API.Common.Paging;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Dashboard;
using ASMARA.API.Services.TicketFlow;
using MediatR;

namespace ASMARA.API.Features.Dashboard.Queries.GetMyActivityTickets;

public record GetMyActivityTicketsQuery(TicketTypeEnum? Type = null, string? Search = null, DateOnly? FromDate = null, 
    DateOnly? ToDate = null, int Page = 1, int PageSize = 10)
    : IRequest<PagedResult<ActivityTicketListItemDto>>;

public class GetMyActivityTicketsQueryHandler(ITicketRepository tickets, IWorkflowStepRepository workflowSteps, ICurrentUser currentUser)
    : IRequestHandler<GetMyActivityTicketsQuery, PagedResult<ActivityTicketListItemDto>>
{
    public async Task<PagedResult<ActivityTicketListItemDto>> Handle(GetMyActivityTicketsQuery request, CancellationToken ct)
    {
        var me = currentUser.EmployeeId;

        var query = tickets.QueryNoTracking()
            .Where(t => t.EmployeeId != me
                     && t.TicketWorkflowLogs.Any(l => l.EmployeeId == me));

        if (request.Type.HasValue)
        {
            var type = (byte)request.Type.Value;
            query = query.Where(t => t.TicketType == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(t => t.TicketNumber.Contains(search)
                                  || t.Subject.Contains(search)
                                  || t.Employee.FullName.Contains(search));
        }

        if (request.FromDate.HasValue || request.ToDate.HasValue)
        {
            var fromUtc = request.FromDate.HasValue
                ? ToUtc(request.FromDate.Value)
                : DateTime.MinValue;

            var toUtcExclusive = request.ToDate.HasValue
                ? ToUtc(request.ToDate.Value.AddDays(1))  
                : DateTime.MaxValue;

            query = query.Where(t => t.TicketWorkflowLogs.Any(l =>
                l.EmployeeId == me
                && l.CreatedAt >= fromUtc
                && l.CreatedAt < toUtcExclusive));
        }

        var steps = workflowSteps.QueryNoTracking();

        var page = await query
            .Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.TicketType,
                t.Subject,
                RequesterName = t.Employee.FullName,
                CurrentStep = t.TicketWorkflowLogs
                    .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                    .Select(l => l.ToStep)
                    .FirstOrDefault() ?? StepCodes.Created,
                MyLast = t.TicketWorkflowLogs
                    .Where(l => l.EmployeeId == me)
                    .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                    .Select(l => new { l.FromStep, l.ToStep, l.CreatedAt })
                    .First()
            })
            .Select(x => new
            {
                x.Id,
                x.TicketNumber,
                x.TicketType,
                x.Subject,
                x.RequesterName,
                x.CurrentStep,
                x.MyLast,
                CurrentStepName = steps
                    .Where(s => s.StepCode == x.CurrentStep
                             && s.Workflow.TicketType == x.TicketType
                             && s.Workflow.IsActive)
                    .Select(s => s.Name)
                    .FirstOrDefault() ?? x.CurrentStep
            })
            .OrderByDescending(x => x.MyLast.CreatedAt)   
            .ToPagedResultAsync(request.Page, request.PageSize, ct: ct);

        var items = page.Items.Select(x =>
        {
            var ticketType = (TicketTypeEnum)x.TicketType;
            var action = TicketHistoryMapper.ResolveAction(ticketType, x.MyLast.FromStep, x.MyLast.ToStep);

            return new ActivityTicketListItemDto(
                x.Id,
                x.TicketNumber,
                x.TicketType,
                x.Subject,
                x.RequesterName,
                x.CurrentStep,
                x.CurrentStepName,
                TicketHistoryMapper.LabelOf(action, x.MyLast.FromStep, x.MyLast.ToStep),
                x.MyLast.CreatedAt);
        }).ToList();

        return new PagedResult<ActivityTicketListItemDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private static DateTime ToUtc(DateOnly date)
        => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
}