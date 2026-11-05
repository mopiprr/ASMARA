using ASMARA.API.Common.Paging;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;

namespace ASMARA.API.Features.Tickets.Queries.GetAllMyTicketsQuery;

public record GetAllMyTicketsQuery(
    TicketTypeEnum Type, string? Search, string? SortBy, int Page = 1, int PageSize = 10, bool Desc = false)
    : IRequest<PagedResult<TicketListItemDto>>;

public class GetAllMyTicketsQueryHandler(ITicketRepository tickets, IWorkflowStepRepository workflowSteps, ICurrentUser currentUser)
    : IRequestHandler<GetAllMyTicketsQuery, PagedResult<TicketListItemDto>>
{
    public async Task<PagedResult<TicketListItemDto>> Handle(GetAllMyTicketsQuery request, CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;
        var type = (byte)request.Type;

        var query = tickets.QueryNoTracking()
            .Where(t => t.EmployeeId == employeeId && t.TicketType == type)
            .Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.Subject,
                t.CreatedAt,
                CurrentStep = t.TicketWorkflowLogs
                    .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                    .Select(l => l.ToStep)
                    .FirstOrDefault() ?? StepCodes.Created
            });

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.TicketNumber.Contains(search) || x.Subject.Contains(search));
        }

        query = (request.SortBy?.ToLower()) switch
        {
            "ticketnumber" => request.Desc ? query.OrderByDescending(x => x.TicketNumber) : query.OrderBy(x => x.TicketNumber),
            "subject" => request.Desc ? query.OrderByDescending(x => x.Subject) : query.OrderBy(x => x.Subject),
            "status" => request.Desc ? query.OrderByDescending(x => x.CurrentStep) : query.OrderBy(x => x.CurrentStep),
            "date" => request.Desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            _ => request.Desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
        };

        var steps = workflowSteps.QueryNoTracking();

        return await query
            .Select(x => new TicketListItemDto(
                x.Id,
                x.TicketNumber,
                x.Subject,
                x.CreatedAt,
                x.CurrentStep,
                steps.Where(s => s.StepCode == x.CurrentStep
                              && s.Workflow.TicketType == type
                              && s.Workflow.IsActive)
                     .Select(s => s.Name)
                     .FirstOrDefault() ?? "Created"))
            .ToPagedResultAsync(request.Page, request.PageSize, ct: ct);
    }
}