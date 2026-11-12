
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

        // =========================================
        // 1. GET ACTIVE TICKET
        // =========================================

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

        // =========================================
        // 2. GET CURRENT WORKFLOW STEP
        // =========================================

        var currentStep = ticket.TicketWorkflowLogs
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Select(log => log.ToStep)
            .FirstOrDefault() ?? StepCodes.Created;

        // =========================================
        // 3. GET WORKFLOW LEVEL
        // =========================================

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

        // =========================================
        // 4. CHECK ASSIGNED HANDLER
        // =========================================

        var isAssignedHandler =
            ticket.AssignedToEmployeeId == employeeId;

        var isHandlerStep =
            currentStep == StepCodes.HandlerReceiveItem ||
            currentStep == StepCodes.RepairInProgress ||
            currentStep == StepCodes.HandlerReceivePart ||
            currentStep == StepCodes.HandlerReturnItem;

        // =========================================
        // 5. CHECK TICKET VISIBILITY
        // =========================================

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
            canView =
                ticket.CurrentTaskEmployeeId == employeeId ||
                (isAssignedHandler && isHandlerStep) ||
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

        // =========================================
        // 6. GET TICKET DETAIL
        // =========================================

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

        // =========================================
        // 7. INITIALIZE PERMISSIONS
        // =========================================

        var canApprove = false;
        var canRevise = false;
        var canReject = false;
        var canReceiveGoods = false;
        var canResubmit = false;

        var canAssignHandler = false;
        var canSendItem = false;
        var canReceiveItem = false;

        var canSubmitRepairResult = false;
        var canReceivePart = false;

        var canCheckPart = false;
        var canOrderPart = false;
        var canSendPart = false;

        var canReviewRepairResult = false;

        // Division Manager
        var canArchive = false;
        var canDispose = false;

        // Inventory Replacement
        var canProcessReplacement = false;
        var canSendReplacement = false;

        // =========================================
        // 8. PROCUREMENT PERMISSIONS
        // =========================================

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

        // =========================================
        // 9. MAINTENANCE PERMISSIONS
        // =========================================

        else if (ticketType == TicketTypeEnum.Maintenance)
        {
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

            // -------------------------------------
            // Department Manager
            // -------------------------------------

            canApprove = Can(TicketActionEnum.Approve);
            canReject = Can(TicketActionEnum.Reject);

            // -------------------------------------
            // Division Manager
            // -------------------------------------

            canArchive =
                currentStep == StepCodes.DivisionManagerReview &&
                Can(TicketActionEnum.Archive);

            canDispose =
                currentStep == StepCodes.DivisionManagerReview &&
                Can(TicketActionEnum.Dispose);

            // -------------------------------------
            // IT Support
            // -------------------------------------

            canAssignHandler = Can(TicketActionEnum.AssignHandler);

            canReviewRepairResult =
                Can(TicketActionEnum.Approve) &&
                currentStep == StepCodes.ItSupportResultReview;

            // -------------------------------------
            // Requester / Handler
            // -------------------------------------

            canSendItem =
                Can(TicketActionEnum.Send) ||
                (
                    isAssignedHandler &&
                    currentStep == StepCodes.HandlerReturnItem
                );

            canReceiveItem =
                Can(TicketActionEnum.Receive) ||
                (
                    isAssignedHandler &&
                    currentStep == StepCodes.HandlerReceiveItem
                );

            // -------------------------------------
            // Maintenance Handler Decision
            // -------------------------------------

            canSubmitRepairResult =
                isAssignedHandler &&
                currentStep == StepCodes.RepairInProgress &&
                (
                    allowedActions.Contains(TicketActionEnum.Repaired) ||
                    allowedActions.Contains(TicketActionEnum.Unrepairable) ||
                    allowedActions.Contains(TicketActionEnum.NeedPart)
                );

            // -------------------------------------
            // Handler Receives Spare Part
            // -------------------------------------

            canReceivePart =
                isAssignedHandler &&
                currentStep == StepCodes.HandlerReceivePart &&
                allowedActions.Contains(TicketActionEnum.Receive);

            // -------------------------------------
            // Inventory - Spare Part
            // -------------------------------------

            canCheckPart =
                Can(TicketActionEnum.PartAvailable) ||
                Can(TicketActionEnum.PartUnavailable);

            canOrderPart = Can(TicketActionEnum.OrderPart);

            canSendPart = Can(TicketActionEnum.SendPart);

            // -------------------------------------
            // Inventory - Replacement Decision
            // -------------------------------------

            var isInventoryReplacementStep =
                currentStep == StepCodes.InventoryDispose ||
                currentStep == StepCodes.InventoryArchive;

            canProcessReplacement =
                isInventoryReplacementStep &&
                Can(TicketActionEnum.ReplacementAvailable) &&
                Can(TicketActionEnum.ReplacementUnavailable);

            // -------------------------------------
            // Inventory - Send Replacement
            // -------------------------------------

            canSendReplacement =
                currentStep == StepCodes.InventorySendReplacement &&
                Can(TicketActionEnum.Send);
        }

        // =========================================
        // 10. WORKFLOW HISTORY
        // =========================================

        var historyItems = await history.GetHistoryAsync(
            query.Id,
            ct);

        // =========================================
        // 11. DEBUG PERMISSIONS
        // =========================================

        Console.WriteLine("========== PENDING TICKET PERMISSION DEBUG ==========");
        Console.WriteLine($"Ticket Number           : {ticket.TicketNumber}");
        Console.WriteLine($"Ticket Type             : {ticketType}");
        Console.WriteLine($"Current Step            : {currentStep}");
        Console.WriteLine($"Login Employee ID       : {employeeId}");
        Console.WriteLine($"Assigned Handler ID     : {ticket.AssignedToEmployeeId}");
        Console.WriteLine($"Current Task Employee ID: {ticket.CurrentTaskEmployeeId}");
        Console.WriteLine($"Step Level              : {stepLevel}");
        Console.WriteLine($"Is Assigned Handler     : {isAssignedHandler}");
        Console.WriteLine($"Can Approve             : {canApprove}");
        Console.WriteLine($"Can Reject              : {canReject}");
        Console.WriteLine($"Can Assign Handler      : {canAssignHandler}");
        Console.WriteLine($"Can Submit Repair Result: {canSubmitRepairResult}");
        Console.WriteLine($"Can Receive Part        : {canReceivePart}");
        Console.WriteLine($"Can Review Repair       : {canReviewRepairResult}");
        Console.WriteLine($"Can Check Part          : {canCheckPart}");
        Console.WriteLine($"Can Order Part          : {canOrderPart}");
        Console.WriteLine($"Can Send Part           : {canSendPart}");
        Console.WriteLine($"Can Archive             : {canArchive}");
        Console.WriteLine($"Can Dispose             : {canDispose}");
        Console.WriteLine($"Can Process Replacement : {canProcessReplacement}");
        Console.WriteLine($"Can Send Replacement    : {canSendReplacement}");
        Console.WriteLine("=====================================================");

        // =========================================
        // 12. RETURN DTO
        // =========================================

        return dto with
        {
            // Procurement
            CanApprove = canApprove,
            CanRevise = canRevise,
            CanReject = canReject,
            CanReceiveGoods = canReceiveGoods,
            CanResubmit = canResubmit,

            // Maintenance
            CanAssignHandler = canAssignHandler,
            CanSendItem = canSendItem,
            CanReceiveItem = canReceiveItem,

            // Handler
            CanSubmitRepairResult = canSubmitRepairResult,
            CanReceivePart = canReceivePart,

            // Inventory
            CanCheckPart = canCheckPart,
            CanOrderPart = canOrderPart,
            CanSendPart = canSendPart,

            // IT Support
            CanReviewRepairResult = canReviewRepairResult,

            // Division Manager
            CanArchive = canArchive,
            CanDispose = canDispose,

            // Inventory Replacement
            CanProcessReplacement = canProcessReplacement,
            CanSendReplacement = canSendReplacement,

            History = historyItems
        };
    }

    // =========================================
    // PROCUREMENT PENDING ACCESS
    // =========================================

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
