
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Queries.GetTicketDetail;

public record GetTicketByIdQuery(Guid Id) : IRequest<TicketResponseDto>;

public class GetTicketByIdQueryHandler(
    IUnitOfWork uow,
    ITicketHistoryService history,
    ICurrentUser currentUser
) : IRequestHandler<GetTicketByIdQuery, TicketResponseDto>
{
    public async Task<TicketResponseDto> Handle(
        GetTicketByIdQuery query,
        CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;
        var roles = currentUser.Roles;

        // ==========================================
        // GET TICKET
        // ==========================================

        var ticket = await uow.Tickets.QueryNoTracking()
            .FirstOrDefaultAsync(t => t.Id == query.Id, ct);

        if (ticket == null)
        {
            throw new NotFoundException(
                $"Ticket {query.Id} was not found");
        }

        var ticketType = (TicketTypeEnum)ticket.TicketType;

        // ==========================================
        // CURRENT WORKFLOW STEP
        // ==========================================

        var currentStep = await uow.Tickets.GetCurrentStepAsync(
            query.Id,
            ct
        ) ?? StepCodes.Created;

        var currentStepLevel = await GetStepLevelAsync(
            ticket.TicketType,
            currentStep,
            ct
        );

        // ==========================================
        // EMPLOYEE ROLE CHECK
        // ==========================================

        var isDivisionManager = roles.Any(role =>
            string.Equals(
                role,
                Roles.DivisionManager,
                StringComparison.OrdinalIgnoreCase));

        var isAssignedHandler =
            ticket.AssignedToEmployeeId == employeeId;

        // ==========================================
        // DIVISION MANAGER ROLE CHECK
        // ==========================================

        bool CanActOnCurrentStep(int? stepLevel)
        {
            var normalAccess = TaskAccess.CanActOn(
                ticket,
                stepLevel,
                employeeId,
                roles
            );

            var divisionManagerAccess =
                ticketType == TicketTypeEnum.Maintenance
                && string.Equals(
                    currentStep,
                    StepCodes.DivisionManagerReview,
                    StringComparison.OrdinalIgnoreCase)
                && stepLevel == StepLevels.DivisionManager
                && isDivisionManager;

            return normalAccess || divisionManagerAccess;
        }

        // ==========================================
        // HANDLER SPECIAL ACCESS
        // ==========================================

        var canAccessHandlerOrderPart =
            ticketType == TicketTypeEnum.Maintenance
            && string.Equals(
                currentStep,
                StepCodes.HandlerOrderPart,
                StringComparison.OrdinalIgnoreCase
            )
            && isAssignedHandler;

        var canAccessHandlerReceivePart =
            ticketType == TicketTypeEnum.Maintenance
            && string.Equals(
                currentStep,
                StepCodes.HandlerReceivePart,
                StringComparison.OrdinalIgnoreCase
            )
            && isAssignedHandler;

        // ==========================================
        // CHECK TICKET VISIBILITY
        // ==========================================

        var canView =
            ticket.EmployeeId == employeeId
            || await uow.TicketWorkflowLogs.QueryNoTracking()
                .AnyAsync(
                    l => l.TicketId == query.Id
                      && l.EmployeeId == employeeId,
                    ct
                )
            || CanActOnCurrentStep(currentStepLevel)
            || canAccessHandlerOrderPart
            || canAccessHandlerReceivePart;

        if (!canView)
        {
            throw new ForbiddenException(
                "You are not allowed to access it.");
        }

        // ==========================================
        // CHECK PERMISSION FOR SPECIFIC STEP
        // ==========================================

        async Task<bool> CanActAtStepAsync(string expectedStep)
        {
            if (!string.Equals(
                currentStep,
                expectedStep,
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var stepLevel = await GetStepLevelAsync(
                ticket.TicketType,
                expectedStep,
                ct
            );

            return CanActOnCurrentStep(stepLevel);
        }

        // ==========================================
        // GENERAL WORKFLOW PERMISSIONS
        // ==========================================

        var canAct = CanActOnCurrentStep(currentStepLevel);

        var allowedActions = TransitionMap.AllowedActions(
            ticketType,
            currentStep
        );

        bool Can(TicketActionEnum action)
        {
            return canAct && allowedActions.Contains(action);
        }

        // Department Manager
        var canApprove = Can(TicketActionEnum.Approve);
        var canReject = Can(TicketActionEnum.Reject);

        // Division Manager
        var canArchive = Can(TicketActionEnum.Archive);
        var canDispose = Can(TicketActionEnum.Dispose);

        // IT Support
        var canAssignHandler = Can(TicketActionEnum.AssignHandler);

        // Requester and Handler
        var canSendItem = Can(TicketActionEnum.Send);
        var canReceiveItem = Can(TicketActionEnum.Receive);

        // ==========================================
        // MAINTENANCE HANDLER
        // ==========================================

        var canSubmitRepairResult =
            ticketType == TicketTypeEnum.Maintenance
            && string.Equals(
                currentStep,
                StepCodes.RepairInProgress,
                StringComparison.OrdinalIgnoreCase
            )
            && isAssignedHandler;

        var canReceivePart = canAccessHandlerReceivePart;

        // ==========================================
        // INVENTORY - SPARE PART
        // ==========================================

        var canCheckPart =
            ticketType == TicketTypeEnum.Maintenance
            && await CanActAtStepAsync(
                StepCodes.InventoryPartCheck);

        // Handler creates Procurement after Inventory selects Not Available.
        var canOrderPart = canAccessHandlerOrderPart;

        var canSendPart =
            ticketType == TicketTypeEnum.Maintenance
            && await CanActAtStepAsync(
                StepCodes.InventorySendPart);

        // ==========================================
        // INVENTORY - REPLACEMENT ASSET
        // ==========================================

        var canProcessReplacement =
            ticketType == TicketTypeEnum.Maintenance
            &&
            (
                await CanActAtStepAsync(
                    StepCodes.InventoryDispose)
                ||
                await CanActAtStepAsync(
                    StepCodes.InventoryArchive)
            )
            &&
            allowedActions.Contains(
                TicketActionEnum.ReplacementAvailable)
            &&
            allowedActions.Contains(
                TicketActionEnum.ReplacementUnavailable);

        var canSendReplacement =
            ticketType == TicketTypeEnum.Maintenance
            &&
            await CanActAtStepAsync(
                StepCodes.InventorySendReplacement)
            &&
            allowedActions.Contains(TicketActionEnum.Send);

        // ==========================================
        // INVENTORY DEBUG
        // ==========================================

        Console.WriteLine("========== INVENTORY ACCESS DEBUG ==========");
        Console.WriteLine($"Current Step       : {currentStep}");
        Console.WriteLine($"Current Step Level : {currentStepLevel}");
        Console.WriteLine($"Inventory Level    : {StepLevels.Inventory}");
        Console.WriteLine(
            $"Is Role Level      : {(currentStepLevel.HasValue && LevelRoleMap.IsRoleLevel(currentStepLevel.Value))}");
        Console.WriteLine($"Login Roles        : {string.Join(", ", roles)}");
        Console.WriteLine($"Inventory Role     : {Roles.Inventory}");
        Console.WriteLine(
            $"Has Inventory Role : {roles.Any(r => string.Equals(r, Roles.Inventory, StringComparison.OrdinalIgnoreCase))}");
        Console.WriteLine($"Can Check Part     : {canCheckPart}");
        Console.WriteLine($"Can Order Part     : {canOrderPart}");
        Console.WriteLine($"Can Send Part      : {canSendPart}");
        Console.WriteLine("============================================");

        // ==========================================
        // IT SUPPORT
        // ==========================================

        var canReviewRepairResult =
            await CanActAtStepAsync(
                StepCodes.ItSupportResultReview);

        // ==========================================
        // BUILD TICKET RESPONSE
        // ==========================================

        var dto = await uow.Tickets.QueryNoTracking()
            .Where(t => t.Id == query.Id)
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
                        t.MaintenanceDetail.CreatedAt
                    ),

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
                        p.CreatedAt
                    ))
                    .ToList()
            ))
            .FirstAsync(ct);

        // ==========================================
        // WORKFLOW HISTORY
        // ==========================================

        var historyItems = await history.GetHistoryAsync(
            query.Id,
            ct
        );

        // ==========================================
        // DEBUG PERMISSIONS
        // ==========================================

        Console.WriteLine();
        Console.WriteLine("========== TICKET PERMISSION DEBUG ==========");
        Console.WriteLine($"Ticket Number           : {ticket.TicketNumber}");
        Console.WriteLine($"Ticket Type             : {ticketType}");
        Console.WriteLine($"Current Step            : {currentStep}");
        Console.WriteLine($"Login Employee ID       : {employeeId}");
        Console.WriteLine($"Current Task ID         : {ticket.CurrentTaskEmployeeId}");
        Console.WriteLine($"Assigned Handler ID     : {ticket.AssignedToEmployeeId}");
        Console.WriteLine($"Step Level              : {currentStepLevel}");
        Console.WriteLine($"Division Manager Role   : {Roles.DivisionManager}");
        Console.WriteLine($"Login Roles             : {string.Join(", ", roles)}");
        Console.WriteLine($"Is Division Manager     : {isDivisionManager}");
        Console.WriteLine($"Is Assigned Handler     : {isAssignedHandler}");
        Console.WriteLine($"Can Act                 : {canAct}");
        Console.WriteLine($"Allowed Actions         : {string.Join(", ", allowedActions)}");
        Console.WriteLine($"Can Approve             : {canApprove}");
        Console.WriteLine($"Can Reject              : {canReject}");
        Console.WriteLine($"Can Archive             : {canArchive}");
        Console.WriteLine($"Can Dispose             : {canDispose}");
        Console.WriteLine($"Can Assign Handler      : {canAssignHandler}");
        Console.WriteLine($"Can Send Item           : {canSendItem}");
        Console.WriteLine($"Can Receive Item        : {canReceiveItem}");
        Console.WriteLine($"Can Submit Repair       : {canSubmitRepairResult}");
        Console.WriteLine($"Can Check Part          : {canCheckPart}");
        Console.WriteLine($"Can Order Part          : {canOrderPart}");
        Console.WriteLine($"Can Send Part           : {canSendPart}");
        Console.WriteLine($"Can Receive Part        : {canReceivePart}");
        Console.WriteLine($"Can Process Replacement : {canProcessReplacement}");
        Console.WriteLine($"Can Send Replacement    : {canSendReplacement}");
        Console.WriteLine($"Can Review Repair       : {canReviewRepairResult}");
        Console.WriteLine("=============================================");
        Console.WriteLine();

        // ==========================================
        // RETURN DTO WITH PERMISSIONS
        // ==========================================

        return dto with
        {
            CurrentStep = currentStep,
            History = historyItems,

            // Department Manager
            CanApprove = canApprove,
            CanReject = canReject,

            // Division Manager
            CanArchive = canArchive,
            CanDispose = canDispose,

            // IT Support
            CanAssignHandler = canAssignHandler,
            CanReviewRepairResult = canReviewRepairResult,

            // Requester and Handler
            CanSendItem = canSendItem,
            CanReceiveItem = canReceiveItem,

            // Maintenance Handler
            CanSubmitRepairResult = canSubmitRepairResult,
            CanReceivePart = canReceivePart,

            // Spare Part Workflow
            CanCheckPart = canCheckPart,
            CanOrderPart = canOrderPart,
            CanSendPart = canSendPart,

            // Replacement Workflow
            CanProcessReplacement = canProcessReplacement,
            CanSendReplacement = canSendReplacement
        };
    }

    // ==========================================
    // GET WORKFLOW STEP LEVEL
    // ==========================================

    private async Task<int?> GetStepLevelAsync(
        byte ticketType,
        string stepCode,
        CancellationToken ct)
    {
        if (string.Equals(
            stepCode,
            StepCodes.Created,
            StringComparison.OrdinalIgnoreCase))
        {
            return StepLevels.Requester;
        }

        var databaseLevel = await uow.WorkflowSteps.QueryNoTracking()
            .Where(s =>
                s.StepCode == stepCode
                && s.Workflow.TicketType == ticketType
                && s.Workflow.IsActive
            )
            .Select(s => (int?)s.Level)
            .FirstOrDefaultAsync(ct);

        // Fallback untuk Division Manager.
        if (databaseLevel is null &&
            string.Equals(
                stepCode,
                StepCodes.DivisionManagerReview,
                StringComparison.OrdinalIgnoreCase))
        {
            return StepLevels.DivisionManager;
        }

        return databaseLevel;
    }
}
