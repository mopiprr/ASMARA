using ASMARA.API.Common.Paging;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Queries.GetCompletedTickets;

public record GetCompletedTicketsQuery(
    bool OnlyMyTickets = false,
    TicketTypeEnum? Type = null,
    string? Search = null,
    string? SortBy = null,
    int Page = 1,
    int PageSize = 10,
    bool Desc = false)
    : IRequest<PagedResult<CompletedTicketListItemDto>>;

public class GetCompletedTicketsQueryHandler(
    ITicketRepository tickets,
    IWorkflowStepRepository workflowSteps,
    ICurrentUser currentUser)
    : IRequestHandler<GetCompletedTicketsQuery, PagedResult<CompletedTicketListItemDto>>
{
    public async Task<PagedResult<CompletedTicketListItemDto>> Handle(GetCompletedTicketsQuery request, CancellationToken ct)
    {
        var query = tickets.QueryNoTracking()
            .Where(t => t.ClosedAt != null);

        if (request.OnlyMyTickets)
        {
            var employeeId = currentUser.EmployeeId;
            query = query.Where(t => t.EmployeeId == employeeId);
        }

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

        query = (request.SortBy?.ToLower()) switch
        {
            "ticketnumber" => request.Desc ? query.OrderByDescending(x => x.TicketNumber) : query.OrderBy(x => x.TicketNumber),
            "subject" => request.Desc ? query.OrderByDescending(x => x.Subject) : query.OrderBy(x => x.Subject),
            "requester" => request.Desc ? query.OrderByDescending(x => x.Employee.FullName) : query.OrderBy(x => x.Employee.FullName),
            "date" => request.Desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            "closedat" => request.Desc ? query.OrderByDescending(x => x.ClosedAt) : query.OrderBy(x => x.ClosedAt),
            _ => request.Desc ? query.OrderByDescending(x => x.ClosedAt ?? x.CreatedAt) : query.OrderByDescending(x => x.ClosedAt ?? x.CreatedAt)
        };

        var steps = workflowSteps.QueryNoTracking();

        var projected = query.Select(t => new
        {
            t.Id,
            t.TicketNumber,
            t.TicketType,
            t.Subject,
            RequesterId = t.EmployeeId,
            RequesterName = t.Employee.FullName,
            t.CreatedAt,
            t.ClosedAt,
            CurrentStep = t.TicketWorkflowLogs
                .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                .Select(l => l.ToStep)
                .FirstOrDefault() ?? StepCodes.Created
        });

        return await projected
            .Select(x => new CompletedTicketListItemDto(
                x.Id,
                x.TicketNumber,
                x.TicketType,
                x.TicketType == (byte)TicketTypeEnum.Maintenance ? "Maintenance" : "Procurement",
                x.Subject,
                x.RequesterId,
                x.RequesterName,
                x.CreatedAt,
                x.ClosedAt,
                x.CurrentStep,
                steps.Where(s => s.StepCode == x.CurrentStep
                              && s.Workflow.TicketType == x.TicketType
                              && s.Workflow.IsActive)
                     .Select(s => s.Name)
                     .FirstOrDefault() ?? x.CurrentStep))
            .ToPagedResultAsync(request.Page, request.PageSize, ct: ct);
    }
}
