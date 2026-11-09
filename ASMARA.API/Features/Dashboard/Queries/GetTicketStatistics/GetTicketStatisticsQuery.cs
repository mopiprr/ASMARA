using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Dashboard;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Dashboard.Queries.GetTicketStatistics;

public record GetTicketStatisticsQuery(
    TicketTypeEnum? Type = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null) 
    : IRequest<TicketStatisticsDto>;

public class GetTicketStatisticsQueryHandler(
    IUnitOfWork uow,
    ICurrentUser currentUser)
    : IRequestHandler<GetTicketStatisticsQuery, TicketStatisticsDto>
{
    public async Task<TicketStatisticsDto> Handle(
        GetTicketStatisticsQuery request,
        CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;
        var roles = currentUser.Roles;
        var roleLevels = LevelRoleMap.LevelsFor(roles);

        var baseQuery = uow.Tickets.QueryNoTracking();

        if (request.Type.HasValue)
        {
            var filterType = (byte)request.Type.Value;
            baseQuery = baseQuery.Where(t => t.TicketType == filterType);
        }

        if (request.StartDate.HasValue)
        {
            var startDateUtc = DateTime.SpecifyKind(request.StartDate.Value.Date, DateTimeKind.Utc);
            baseQuery = baseQuery.Where(t => t.CreatedAt >= startDateUtc);
        }

        if (request.EndDate.HasValue)
        {
            var nextDayUtc = DateTime.SpecifyKind(request.EndDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            baseQuery = baseQuery.Where(t => t.CreatedAt < nextDayUtc);
        }

        // Ambil workflowstep aktif
        var activeSteps = await uow.WorkflowSteps.QueryNoTracking()
            .Where(s => s.Workflow.IsActive)
            .Select(s => new
            {
                s.Workflow.TicketType,
                s.StepCode,
                s.Level
            })
            .ToListAsync(ct);

        var stepLookup = activeSteps.ToDictionary(
            s => (s.TicketType, s.StepCode),
            s => s.Level);

        // Ambil semua tiket aktif berdasarkan closedAt = null
        var openTickets = await baseQuery
            .Where(t => t.ClosedAt == null)
            .Include(t => t.TicketWorkflowLogs)
            .ToListAsync(ct);

        var pendingTicketIds = new HashSet<Guid>();

        foreach (var ticket in openTickets)
        {
            var currentStep = ticket.TicketWorkflowLogs
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .Select(log => log.ToStep)
                .FirstOrDefault() ?? StepCodes.Created;

            int? stepLevel = currentStep == StepCodes.Created
                ? StepLevels.Requester
                : stepLookup.TryGetValue((ticket.TicketType, currentStep), out var lvl) ? lvl : null;

            var ticketType = (TicketTypeEnum)ticket.TicketType;
            var isPendingForUser = false;

            // Evaluasi apakah tiket sedang menunggu aksi user yang sedang login
            if (ticketType == TicketTypeEnum.Procurement)
            {
                isPendingForUser = await CanViewProcurementPendingAsync(
                    ticket, currentStep, stepLevel, employeeId, roles, ct);
            }
            else
            {
                isPendingForUser = ticket.CurrentTaskEmployeeId == employeeId ||
                    (
                        stepLevel.HasValue &&
                        roleLevels.Contains(stepLevel.Value) &&
                        (
                            stepLevel.Value != StepLevels.Finance ||
                            ticket.EmployeeId != employeeId
                        )
                    );
            }

            if (isPendingForUser)
            {
                pendingTicketIds.Add(ticket.Id);
            }
        }

        // Pending
        var pendingCount = pendingTicketIds.Count;

        // Tiket milik user yang sedang login
        var myTicketsQuery = baseQuery.Where(t => t.EmployeeId == employeeId);
        var totalCount = await myTicketsQuery.CountAsync(ct);
        var inProgressCount = await myTicketsQuery.Where(t => t.ClosedAt == null).CountAsync(ct);
        var completedCount = await myTicketsQuery.Where(t => t.ClosedAt != null).CountAsync(ct);

        return new TicketStatisticsDto(
            TotalTickets: totalCount,
            PendingTickets: pendingCount,
            InProgressTickets: inProgressCount,
            CompletedTickets: completedCount
        );
    }

    private async Task<bool> CanViewProcurementPendingAsync(
        Ticket ticket,
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
            uow.Employees,
            ct);
    }
}
