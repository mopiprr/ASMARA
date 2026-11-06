
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
    ITicketRepository tickets,
    IWorkflowStepRepository workflowSteps,
    IEmployeeRepository employees,
    ICurrentUser currentUser)
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
        var ticket = await tickets.QueryNoTracking()
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
            stepLevel = await workflowSteps.QueryNoTracking()
                .Where(s =>
                    s.StepCode == currentStep &&
                    s.Workflow.TicketType == ticket.TicketType &&
                    s.Workflow.IsActive)
                .Select(s => (int?)s.Level)
                .FirstOrDefaultAsync(ct);
        }

        var ticketType = (TicketTypeEnum)ticket.TicketType;

        // 4. Validasi hak akses user.
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
        var dto = await tickets.QueryNoTracking()
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

        // 6. Tentukan hak akses tombol Procurement.
        var canApprove = false;
        var canRevise = false;
        var canReject = false;
        var canReceiveGoods = false;
        var canResubmit = false;

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
                    employees,
                    ct);

                canRevise = await TaskAccess.CanActOnAsync(
                    ticket,
                    stepLevel,
                    employeeId,
                    roles,
                    TicketActionEnum.Revise,
                    employees,
                    ct);

                canReject = await TaskAccess.CanActOnAsync(
                    ticket,
                    stepLevel,
                    employeeId,
                    roles,
                    TicketActionEnum.Reject,
                    employees,
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
                    employees,
                    ct);
            }

            // Employee pembuat tiket dapat melakukan Resubmit
            // ketika tiket berada pada tahap EmployeeRevision.
            if (currentStep == StepCodes.EmployeeRevision)
            {
                canResubmit = await TaskAccess.CanActOnAsync(
                    ticket,
                    stepLevel,
                    employeeId,
                    roles,
                    TicketActionEnum.Resubmit,
                    employees,
                    ct);
            }
        }

        // 7. Kembalikan DTO beserta hak akses user.
        return dto with
        {
            CanApprove = canApprove,
            CanRevise = canRevise,
            CanReject = canReject,
            CanReceiveGoods = canReceiveGoods,
            CanResubmit = canResubmit
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
            employees,
            ct);
    }
}
