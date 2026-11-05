using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Queries.GetPendingTickets;

public record GetPendingTicketByIdQuery(Guid Id) : IRequest<TicketResponseDto>;

public class GetPendingTicketByIdQueryHandler(
    ITicketRepository tickets,
    IWorkflowStepRepository workflowSteps,
    ICurrentUser currentUser)
    : IRequestHandler<GetPendingTicketByIdQuery, TicketResponseDto>
{
    public async Task<TicketResponseDto> Handle(GetPendingTicketByIdQuery query, CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;
        var userRoleLevels = LevelRoleMap.LevelsFor(currentUser.Roles).ToList();

        //Filter tiket aktif yang sesuai dengan Id
        var baseQuery = tickets.QueryNoTracking()
            .Where(t => t.Id == query.Id && t.ClosedAt == null);

        //Validasi apakah tiket ini menunggu tindakan dari user ini
        if (userRoleLevels.Count == 0)
        {
            baseQuery = baseQuery.Where(t => t.CurrentTaskEmployeeId == employeeId);
        }
        else
        {
            var activeSteps = workflowSteps.QueryNoTracking()
                .Where(s => s.Workflow.IsActive && s.Level != null && userRoleLevels.Contains(s.Level.Value));

            baseQuery = baseQuery.Where(t =>
                t.CurrentTaskEmployeeId == employeeId
                || (t.CurrentTaskEmployeeId == null
                    && activeSteps.Any(s => s.Workflow.TicketType == t.TicketType
                        && s.StepCode == (t.TicketWorkflowLogs
                            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
                            .Select(l => l.ToStep)
                            .FirstOrDefault() ?? StepCodes.Created)
                        && (!s.Level.HasValue || s.Level.Value != StepLevels.Finance || t.EmployeeId != employeeId))));
        }

        // Ambil detail lengkap tiket
        var dto = await baseQuery
            .Select(t => new TicketResponseDto(
                t.Id,
                t.ParentTicketId,
                t.EmployeeId,
                t.Employee.FullName,
                t.TicketNumber,
                t.TicketType,
                t.Subject,
                t.Description,
                t.AssignedToEmployeeId,
                t.AssignedTo,
                t.CreatedAt,
                t.ClosedAt,
                t.UpdatedAt,
                t.CurrentTaskEmployeeId,
                StepCodes.Created,
                t.MaintenanceDetail == null ? null : new MaintenanceDetailResponseDto(
                    t.MaintenanceDetail.TicketId,
                    t.MaintenanceDetail.AssetId,
                    t.MaintenanceDetail.Asset.AssetTag,
                    t.MaintenanceDetail.Description,
                    t.MaintenanceDetail.RepairResult,
                    t.MaintenanceDetail.HandlerNote,
                    t.MaintenanceDetail.PartRequirement,
                    t.MaintenanceDetail.ReplacementAssetId,
                    t.MaintenanceDetail.ReplacementAsset != null ? t.MaintenanceDetail.ReplacementAsset.AssetTag : null,
                    t.MaintenanceDetail.CreatedAt),
                t.ProcurementDetails
                    .Select(p => new ProcurementDetailResponseDto(
                        p.TicketId,
                        p.ItemId,
                        p.Item != null ? p.Item.ItemCode : null,
                        p.ItemCategoryId,
                        p.ItemCategory.CategoryName,
                        p.ItemName,
                        p.Quantity,
                        p.Price,
                        p.Quantity * p.Price,
                        p.CreatedAt))
                    .ToList()))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Pending ticket {query.Id} was not found or is not awaiting action from you.");

        var currentStep = await tickets.GetCurrentStepAsync(query.Id, ct) ?? StepCodes.Created;
        return dto with { CurrentStep = currentStep };
    }
}
