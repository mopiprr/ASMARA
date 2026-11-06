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
    IEmployeeRepository employees,
    ICurrentUser currentUser)
    : IRequestHandler<GetPendingTicketsQuery, PagedResult<PendingTicketListItemDto>>
{
    public async Task<PagedResult<PendingTicketListItemDto>> Handle(
        GetPendingTicketsQuery request,
        CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;
        var roles = currentUser.Roles;
        var roleLevels = LevelRoleMap.LevelsFor(roles);

        // Ambil tiket yang masih aktif.
        var baseQuery = tickets.QueryNoTracking()
            .Where(t => t.ClosedAt == null);

        if (request.Type.HasValue)
        {
            var type = (byte)request.Type.Value;
            baseQuery = baseQuery.Where(t => t.TicketType == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            baseQuery = baseQuery.Where(t =>
                t.TicketNumber.Contains(search) ||
                t.Subject.Contains(search) ||
                t.Employee.FullName.Contains(search));
        }

        // Urutkan sebelum memeriksa akses.
        baseQuery = ApplySorting(baseQuery, request);

        // Ambil workflow step aktif untuk menentukan level setiap tiket.
        var activeSteps = await workflowSteps.QueryNoTracking()
            .Where(s => s.Workflow.IsActive)
            .Select(s => new
            {
                s.Workflow.TicketType,
                s.StepCode,
                s.Level,
                s.Name
            })
            .ToListAsync(ct);

        var stepLookup = activeSteps.ToDictionary(
            s => (s.TicketType, s.StepCode),
            s => s.Level);

        // Ambil kandidat tiket beserta history workflow.
        var candidates = await baseQuery
            .Include(t => t.TicketWorkflowLogs)
            .ToListAsync(ct);

        var authorizedIds = new List<Guid>();

        foreach (var ticket in candidates)
        {
            var currentStep = ticket.TicketWorkflowLogs
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .Select(log => log.ToStep)
                .FirstOrDefault() ?? StepCodes.Created;

            int? stepLevel = currentStep == StepCodes.Created
                ? StepLevels.Requester
                : stepLookup.TryGetValue(
                    (ticket.TicketType, currentStep),
                    out var foundLevel)
                    ? foundLevel
                    : null;

            var ticketType = (TicketTypeEnum)ticket.TicketType;
            var canView = false;

            if (ticketType == TicketTypeEnum.Procurement)
            {
                canView = await CanViewProcurementPendingAsync(
                    ticket,
                    currentStep,
                    stepLevel,
                    employeeId,
                    roles,
                    ct);
            }
            else
            {
                // Pertahankan aturan Pending Maintenance yang lama.
                canView =
                    ticket.CurrentTaskEmployeeId == employeeId ||
                    (
                        stepLevel.HasValue &&
                        roleLevels.Contains(stepLevel.Value) &&
                        (
                            stepLevel.Value != StepLevels.Finance ||
                            ticket.EmployeeId != employeeId
                        )
                    );
            }

            if (canView)
                authorizedIds.Add(ticket.Id);
        }

        // Jika tidak ada tiket yang dapat diakses, hasil tetap kosong.
        var resultQuery = tickets.QueryNoTracking()
            .Where(t => authorizedIds.Contains(t.Id));

        resultQuery = ApplySorting(resultQuery, request);

        var steps = workflowSteps.QueryNoTracking();

        var projected = resultQuery.Select(t => new
        {
            t.Id,
            t.TicketNumber,
            t.TicketType,
            t.Subject,
            RequesterId = t.EmployeeId,
            RequesterName = t.Employee.FullName,
            t.CreatedAt,
            CurrentStep = t.TicketWorkflowLogs
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .Select(log => log.ToStep)
                .FirstOrDefault() ?? StepCodes.Created
        });

        return await projected
            .Select(t => new PendingTicketListItemDto(
                t.Id,
                t.TicketNumber,
                t.TicketType,
                t.TicketType == (byte)TicketTypeEnum.Maintenance
                    ? "Maintenance"
                    : "Procurement",
                t.Subject,
                t.RequesterId,
                t.RequesterName,
                t.CreatedAt,
                t.CurrentStep,
                steps.Where(s =>
                        s.StepCode == t.CurrentStep &&
                        s.Workflow.TicketType == t.TicketType &&
                        s.Workflow.IsActive)
                    .Select(s => s.Name)
                    .FirstOrDefault() ?? t.CurrentStep
            ))
            .ToPagedResultAsync(
                request.Page,
                request.PageSize,
                ct: ct);
    }

    private async Task<bool> CanViewProcurementPendingAsync(
        Entities.Ticket ticket,
        string currentStep,
        int? stepLevel,
        Guid employeeId,
        IEnumerable<string> roles,
        CancellationToken ct)
    {
        TicketActionEnum? action = currentStep switch
        {
            StepCodes.DepartmentApproval => TicketActionEnum.Approve,
            StepCodes.FinanceApproval => TicketActionEnum.Approve,
            StepCodes.Purchasing => TicketActionEnum.ReceiveGoods,
            StepCodes.EmployeeRevision => TicketActionEnum.Resubmit,
            _ => null
        };

        if (action is null)
            return false;

        return await TaskAccess.CanActOnAsync(
            ticket,
            stepLevel,
            employeeId,
            roles,
            action.Value,
            employees,
            ct);
    }

    private static IQueryable<Entities.Ticket> ApplySorting(
        IQueryable<Entities.Ticket> query,
        GetPendingTicketsQuery request)
    {
        return request.SortBy?.ToLowerInvariant() switch
        {
            "ticketnumber" => request.Desc
                ? query.OrderByDescending(t => t.TicketNumber)
                : query.OrderBy(t => t.TicketNumber),

            "subject" => request.Desc
                ? query.OrderByDescending(t => t.Subject)
                : query.OrderBy(t => t.Subject),

            "requester" => request.Desc
                ? query.OrderByDescending(t => t.Employee.FullName)
                : query.OrderBy(t => t.Employee.FullName),

            "date" => request.Desc
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt),

            _ => request.Desc
                ? query.OrderByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.CreatedAt)
        };
    }
}