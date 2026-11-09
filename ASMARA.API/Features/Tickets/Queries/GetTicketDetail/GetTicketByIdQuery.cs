
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
    ICurrentUser currentUser)
    : IRequestHandler<GetTicketByIdQuery, TicketResponseDto>
{
    public async Task<TicketResponseDto> Handle(
        GetTicketByIdQuery query,
        CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;

        // Get ticket.
        var ticket = await uow.Tickets.QueryNoTracking()
            .FirstOrDefaultAsync(t => t.Id == query.Id, ct);

        if (ticket == null)
            throw new NotFoundException(
                $"Ticket {query.Id} was not found");

        // Get current workflow step.
        var currentStep = await uow.Tickets.GetCurrentStepAsync(
            query.Id,
            ct) ?? StepCodes.Created;

        // Get current workflow level.
        var stepLevel = await GetStepLevelAsync(
            ticket.TicketType,
            currentStep,
            ct);

        // Check whether the current user is allowed to view this ticket.
        var canView = ticket.EmployeeId == employeeId
            || await uow.TicketWorkflowLogs.QueryNoTracking()
                .AnyAsync(
                    l => l.TicketId == query.Id
                         && l.EmployeeId == employeeId,
                    ct)
            || TaskAccess.CanActOn(
                ticket,
                stepLevel,
                employeeId,
                currentUser.Roles);

        if (!canView)
            throw new ForbiddenException(
                "You are not allowed to access it.");

        // Calculate action permissions.
        var canAct = TaskAccess.CanActOn(
            ticket,
            stepLevel,
            employeeId,
            currentUser.Roles);

        var allowedActions = TransitionMap.AllowedActions(
            (TicketTypeEnum)ticket.TicketType,
            currentStep);

        bool Can(TicketActionEnum action)
            => canAct && allowedActions.Contains(action);

        // Temporary debugging for workflow permissions.
        Console.WriteLine("========== TICKET PERMISSION DEBUG ==========");
        Console.WriteLine($"Ticket ID          : {ticket.Id}");
        Console.WriteLine($"Ticket Number      : {ticket.TicketNumber}");
        Console.WriteLine($"Ticket Type        : {ticket.TicketType}");
        Console.WriteLine($"Employee ID        : {employeeId}");
        Console.WriteLine($"Ticket Employee    : {ticket.EmployeeId}");
        Console.WriteLine($"Current Task ID    : {ticket.CurrentTaskEmployeeId}");
        Console.WriteLine($"Current Step       : {currentStep}");
        Console.WriteLine($"Step Level         : {stepLevel}");
        Console.WriteLine($"User Roles         : {string.Join(", ", currentUser.Roles)}");
        Console.WriteLine($"Can View           : {canView}");
        Console.WriteLine($"Can Act            : {canAct}");
        Console.WriteLine($"Allowed Actions    : {string.Join(", ", allowedActions)}");
        Console.WriteLine($"Can Approve        : {Can(TicketActionEnum.Approve)}");
        Console.WriteLine($"Can Assign Handler : {Can(TicketActionEnum.AssignHandler)}");
        Console.WriteLine($"Can Send Item      : {Can(TicketActionEnum.Send)}");
        Console.WriteLine($"Can Receive Item   : {Can(TicketActionEnum.Receive)}");
        Console.WriteLine("==============================================");

        // Get ticket details.
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
            .FirstAsync(ct);

        // Get workflow history.
        var historyItems = await history.GetHistoryAsync(
            query.Id,
            ct);

        // Return ticket with workflow permissions.
        return dto with
        {
            CurrentStep = currentStep,
            History = historyItems,

            CanApprove = Can(TicketActionEnum.Approve),
            CanRevise = Can(TicketActionEnum.Revise),
            CanReject = Can(TicketActionEnum.Reject),
            CanReceiveGoods = Can(TicketActionEnum.ReceiveGoods),
            CanResubmit = Can(TicketActionEnum.Resubmit),

            CanAssignHandler = Can(TicketActionEnum.AssignHandler),
            CanSendItem = Can(TicketActionEnum.Send),
            CanReceiveItem = Can(TicketActionEnum.Receive)
        };
    }

    private async Task<int?> GetStepLevelAsync(
        byte ticketType,
        string stepCode,
        CancellationToken ct)
    {
        // The Created step belongs to the requester.
        if (stepCode == StepCodes.Created)
            return StepLevels.Requester;

        // Find the workflow level for the active step.
        var level = await uow.WorkflowSteps.QueryNoTracking()
            .Where(s =>
                s.StepCode == stepCode
                && s.Workflow.TicketType == ticketType
                && s.Workflow.IsActive)
            .Select(s => (int?)s.Level)
            .FirstOrDefaultAsync(ct);

        if (level == null)
        {
            throw new TicketStateException(
                $"Step '{stepCode}' is not defined in the active workflow.");
        }

        return level;
    }
}
