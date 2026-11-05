using ASMARA.API.Common.Paging;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Queries.GetPendingTickets;

public record GetPendingTicketsQuery(
    TicketTypeEnum? Type = null,
    string? Search = null,
    string? SortBy = null,
    int Page = 1,
    int PageSize = 10,
    bool Desc = false)
    : IRequest<PagedResult<PendingTicketListItemDto>>;

public class GetPendingTicketsQueryHandler(
    ITicketRepository tickets,
    IWorkflowStepRepository workflowSteps,
    ICurrentUser currentUser)
    : IRequestHandler<GetPendingTicketsQuery, PagedResult<PendingTicketListItemDto>>
{
    public async Task<PagedResult<PendingTicketListItemDto>> Handle(GetPendingTicketsQuery request, CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;
        var userRoleLevels = LevelRoleMap.LevelsFor(currentUser.Roles).ToList();

        var query = tickets.QueryNoTracking()
            .Where(t => t.ClosedAt == null);

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

        if (userRoleLevels.Count == 0)
        {
            query = query.Where(t => t.CurrentTaskEmployeeId == employeeId);
        }
        else
        {
            var activeSteps = workflowSteps.QueryNoTracking()
                .Where(s => s.Workflow.IsActive && s.Level != null && userRoleLevels.Contains(s.Level.Value));

            query = query.Where(t =>
                t.CurrentTaskEmployeeId == employeeId
                || (t.CurrentTaskEmployeeId == null
                    && activeSteps.Any(s => s.Workflow.TicketType == t.TicketType
                        && s.StepCode == (t.TicketWorkflowLogs
                            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                            .Select(l => l.ToStep)
                            .FirstOrDefault() ?? StepCodes.Created)
                        && (!s.Level.HasValue || s.Level.Value != StepLevels.Finance || t.EmployeeId != employeeId))));
        }

        query = (request.SortBy?.ToLower()) switch
        {
            "ticketnumber" => request.Desc ? query.OrderByDescending(x => x.TicketNumber) : query.OrderBy(x => x.TicketNumber),
            "subject" => request.Desc ? query.OrderByDescending(x => x.Subject) : query.OrderBy(x => x.Subject),
            "requester" => request.Desc ? query.OrderByDescending(x => x.Employee.FullName) : query.OrderBy(x => x.Employee.FullName),
            "date" => request.Desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt),
            _ => request.Desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
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
            CurrentStep = t.TicketWorkflowLogs
                .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                .Select(l => l.ToStep)
                .FirstOrDefault() ?? StepCodes.Created
        });

        return await projected
            .Select(x => new PendingTicketListItemDto(
                x.Id,
                x.TicketNumber,
                x.TicketType,
                x.TicketType == (byte)TicketTypeEnum.Maintenance ? "Maintenance" : "Procurement",
                x.Subject,
                x.RequesterId,
                x.RequesterName,
                x.CreatedAt,
                x.CurrentStep,
                steps.Where(s => s.StepCode == x.CurrentStep
                              && s.Workflow.TicketType == x.TicketType
                              && s.Workflow.IsActive)
                     .Select(s => s.Name)
                     .FirstOrDefault() ?? x.CurrentStep))
            .ToPagedResultAsync(request.Page, request.PageSize, ct: ct);
    }
}
