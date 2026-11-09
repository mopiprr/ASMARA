
using ASMARA.API.Common.Paging;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;

namespace ASMARA.API.Features.Tickets.Queries.GetAllMyTicketsQuery;

public record GetAllMyTicketsQuery(
    TicketTypeEnum? Type = null,
    string? Search = null,
    string? SortBy = null,
    int Page = 1,
    int PageSize = 10,
    bool Desc = false,
    bool? IsClosed = null)
    : IRequest<PagedResult<TicketListItemDto>>;

public class GetAllMyTicketsQueryHandler(IUnitOfWork uow,
    ICurrentUser currentUser)
    : IRequestHandler<GetAllMyTicketsQuery, PagedResult<TicketListItemDto>>
{
    public async Task<PagedResult<TicketListItemDto>> Handle(
        GetAllMyTicketsQuery request,
        CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;

        var query = uow.Tickets.QueryNoTracking()
            .Where(t => t.EmployeeId == employeeId);

        if (request.IsClosed.HasValue)
        {
            query = request.IsClosed.Value
                ? query.Where(t => t.ClosedAt != null)
                : query.Where(t => t.ClosedAt == null);
        }

        if (request.Type.HasValue)
        {
            var filterType = (byte)request.Type.Value;
            query = query.Where(t => t.TicketType == filterType);
        }

        var projected = query.Select(t => new
        {
            t.Id,
            t.TicketNumber,
            t.Subject,
            t.CreatedAt,
            t.TicketType,
            CurrentStep = t.TicketWorkflowLogs
                .OrderByDescending(l => l.CreatedAt)
                .ThenByDescending(l => l.Id)
                .Select(l => l.ToStep)
                .FirstOrDefault() ?? StepCodes.Created
        });

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            projected = projected.Where(x =>
                x.TicketNumber.Contains(search) ||
                x.Subject.Contains(search));
        }

        projected = request.SortBy?.ToLower() switch
        {
            "ticketnumber" => request.Desc
                ? projected.OrderByDescending(x => x.TicketNumber)
                : projected.OrderBy(x => x.TicketNumber),

            "subject" => request.Desc
                ? projected.OrderByDescending(x => x.Subject)
                : projected.OrderBy(x => x.Subject),

            "status" => request.Desc
                ? projected.OrderByDescending(x => x.CurrentStep)
                : projected.OrderBy(x => x.CurrentStep),

            "date" => request.Desc
                ? projected.OrderByDescending(x => x.CreatedAt)
                : projected.OrderBy(x => x.CreatedAt),

            _ => request.Desc
                ? projected.OrderByDescending(x => x.CreatedAt)
                : projected.OrderBy(x => x.CreatedAt)
        };

        var steps = uow.WorkflowSteps.QueryNoTracking();

        return await projected
            .Select(x => new TicketListItemDto(
                x.Id,
                x.TicketNumber,
                x.Subject,
                x.CreatedAt,
                x.CurrentStep,
                x.TicketType,
                steps.Where(s =>
                        s.StepCode == x.CurrentStep &&
                        s.Workflow.TicketType == x.TicketType &&
                        s.Workflow.IsActive)
                    .Select(s => s.Name)
                    .FirstOrDefault() ?? "Created"))
            .ToPagedResultAsync(
                request.Page,
                request.PageSize,
                ct: ct);
    }
}
