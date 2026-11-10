
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Queries.GetPendingTickets;

public record GetPendingTicketByIdQuery(Guid Id)
    : IRequest<TicketResponseDto>;

public class GetPendingTicketByIdQueryHandler(
    IUnitOfWork uow,
    ICurrentUser currentUser,
    ITicketHistoryService history)
    : IRequestHandler<GetPendingTicketByIdQuery, TicketResponseDto>
{
    public async Task<TicketResponseDto> Handle(
        GetPendingTicketByIdQuery query,
        CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;
        var roles = currentUser.Roles;
        var roleLevels = LevelRoleMap.LevelsFor(roles);

        // 1. Ambil tiket aktif.
        var ticket = await uow.Tickets.QueryNoTracking()
            .Include(t => t.TicketWorkflowLogs)
            .FirstOrDefaultAsync(
                t => t.Id == query.Id && t.ClosedAt == null,
                ct);

        if (ticket is null)
        {
            throw new NotFoundException(
                $"Pending ticket {query.Id} was not found or is not awaiting action from you.");
        }

        // 2. Tentukan current step berdasarkan workflow history.
        var currentStep = ticket.TicketWorkflowLogs
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Select(log => log.ToStep)
            .FirstOrDefault() ?? StepCodes.Created;

        // 3. Ambil level workflow.
        int? stepLevel;

        if (currentStep == StepCodes.Created)
        {
            stepLevel = StepLevels.Requester;
        }
        else
        {
            stepLevel = await uow.WorkflowSteps.QueryNoTracking()
                .Where(s =>
                    s.StepCode == currentStep &&
                    s.Workflow.TicketType == ticket.TicketType &&
                    s.Workflow.IsActive)
                .Select(s => (int?)s.Level)
                .FirstOrDefaultAsync(ct);
        }

        var ticketType = (TicketTypeEnum)ticket.TicketType;

        // 4. Validasi hak akses user untuk melihat pending ticket.
        bool canView;

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
            // Pertahankan mekanisme akses Pending Maintenance.
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

        if (!canView)
        {
            throw new NotFoundException(
                $"Pending ticket {query.Id} was not found or is not awaiting action from you.");
        }

        // 5. Ambil detail tiket.
        var dto = await uow.Tickets.QueryNoTracking()
            .Where(t =>
                t.Id == query.Id &&
                t.ClosedAt == null)
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
                currentStep,

                // Maintenance Detail
                t.MaintenanceDetail == null
                    ? null
                    : new MaintenanceDetailResponseDto(
                        t.MaintenanceDetail.TicketId,
                        t.MaintenanceDetail.AssetId,
                        t.MaintenanceDetail.Asset.AssetTag,
                        t.MaintenanceDetail.Description,
                        t.MaintenanceDetail.RepairResult,
                        t.MaintenanceDetail.HandlerNote,
                        t.MaintenanceDetail.PartRequirement,
                        t.MaintenanceDetail.ReplacementAssetId,
                        t.MaintenanceDetail.ReplacementAsset != null
                            ? t.MaintenanceDetail.ReplacementAsset.AssetTag
                            : null,
                        t.MaintenanceDetail.CreatedAt),

                // Procurement Details
                t.ProcurementDetails
                    .Select(p => new ProcurementDetailResponseDto(
                        p.TicketId,
                        p.ItemId,
                        p.Item != null
                            ? p.Item.ItemCode
                            : null,
                        p.ItemCategoryId,
                        p.ItemCategory.CategoryName,
                        p.ItemName,
                        p.Quantity,
                        p.Price,
                        p.Quantity * p.Price,
                        p.CreatedAt))
                    .ToList()
            ))
            .FirstOrDefaultAsync(ct);

        if (dto is null)
        {
            throw new NotFoundException(
                $"Pending ticket {query.Id} was not found.");
        }

        // 6. Tentukan hak akses tombol berdasarkan jenis tiket.
        var canApprove = false;
        var canRevise = false;
        var canReject = false;
        var canReceiveGoods = false;
        var canResubmit = false;

        var canAssignHandler = false;
        var canSendItem = false;
        var canReceiveItem = false;

        if (ticketType == TicketTypeEnum.Procurement)
        {
            if (currentStep is StepCodes.DepartmentApproval
                or StepCodes.FinanceApproval)
            {
                canApprove = await TaskAccess.CanActOnAsync(
                    ticket,
                    stepLevel,
                    employeeId,
                    roles,
                    TicketActionEnum.Approve,
                    uow.Employees,
                    ct);

                canRevise = await TaskAccess.CanActOnAsync(
                    ticket,
                    stepLevel,
                    employeeId,
                    roles,
                    TicketActionEnum.Revise,
                    uow.Employees,
                    ct);

                canReject = await TaskAccess.CanActOnAsync(
                    ticket,
                    stepLevel,
                    employeeId,
                    roles,
                    TicketActionEnum.Reject,
                    uow.Employees,
                    ct);
            }

            if (currentStep == StepCodes.Purchasing)
            {
                canReceiveGoods = await TaskAccess.CanActOnAsync(
                    ticket,
                    stepLevel,
                    employeeId,
                    roles,
                    TicketActionEnum.ReceiveGoods,
                    uow.Employees,
                    ct);
            }

            if (currentStep == StepCodes.EmployeeRevision)
            {
                canResubmit = await TaskAccess.CanActOnAsync(
                    ticket,
                    stepLevel,
                    employeeId,
                    roles,
                    TicketActionEnum.Resubmit,
                    uow.Employees,
                    ct);
            }
        }
        else if (ticketType == TicketTypeEnum.Maintenance)
        {
            // Hitung hak akses berdasarkan workflow Maintenance.
            var canAct = TaskAccess.CanActOn(
                ticket,
                stepLevel,
                employeeId,
                roles);

            var allowedActions = TransitionMap.AllowedActions(
                ticketType,
                currentStep);

            bool Can(TicketActionEnum action)
                => canAct && allowedActions.Contains(action);

            // Department Manager.
            canApprove = Can(TicketActionEnum.Approve);
            canReject = Can(TicketActionEnum.Reject);

            // IT Support.
            canAssignHandler = Can(TicketActionEnum.AssignHandler);

            // Maintenance workflow actions.
            canSendItem = Can(TicketActionEnum.Send);
            canReceiveItem = Can(TicketActionEnum.Receive);
        }

        var historyItems = await history.GetHistoryAsync(query.Id, ct);

        // 7. Kembalikan DTO beserta hak akses user.
        return dto with
        {
            CanApprove = canApprove,
            CanRevise = canRevise,
            CanReject = canReject,
            CanReceiveGoods = canReceiveGoods,
            CanResubmit = canResubmit,

            CanAssignHandler = canAssignHandler,
            CanSendItem = canSendItem,
            CanReceiveItem = canReceiveItem,
            History = historyItems
        };
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
            StepCodes.DepartmentApproval =>
                TicketActionEnum.Approve,

            StepCodes.FinanceApproval =>
                TicketActionEnum.Approve,

            StepCodes.Purchasing =>
                TicketActionEnum.ReceiveGoods,

            StepCodes.EmployeeRevision =>
                TicketActionEnum.Resubmit,

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
