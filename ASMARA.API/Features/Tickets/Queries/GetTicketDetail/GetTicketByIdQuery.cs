using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Queries;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Queries.GetTicketDetail;

public record GetTicketByIdQuery(Guid Id) : IRequest<TicketResponseDto>;

public class GetTicketByIdQueryHandler(IUnitOfWork uow, ITicketHistoryService history, ICurrentUser currentUser)
    : IRequestHandler<GetTicketByIdQuery, TicketResponseDto>
{
    public async Task<TicketResponseDto> Handle(GetTicketByIdQuery query, CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;

        var ticket = await uow.Tickets.QueryNoTracking()
            .FirstOrDefaultAsync(t => t.Id == query.Id, ct);
        if (ticket == null)
            throw new NotFoundException($"Ticket {query.Id} was not found");

        var currentStep = await uow.Tickets.GetCurrentStepAsync(query.Id, ct) ?? StepCodes.Created;

        var stepLevel = await GetStepLevelAsync(ticket.TicketType, currentStep, ct);
        var canAct = TaskAccess.CanActOn(ticket, stepLevel, employeeId, currentUser.Roles);

        var canView = ticket.EmployeeId == employeeId
                || await uow.TicketWorkflowLogs.QueryNoTracking()
                    .AnyAsync(l => l.TicketId == query.Id && l.EmployeeId == employeeId, ct)
                || canAct;
        if (!canView)
            throw new ForbiddenException($"You are not allowed to access it.");

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
               t.MaintenanceDetail == null ? null : new MaintenanceDetailResponseDto(
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
                   .ToList(),
               null,
               false, false, false, false, false, false, false, false,
               t.PurchaseProofFileName,
               t.Employee.Department != null ? t.Employee.Department.DepartmentName : null
           ))
           .FirstAsync(ct);

        var historyItems = await history.GetHistoryAsync(query.Id, ct);

        var result = dto with { CurrentStep = currentStep, History = historyItems, PurchaseProofFileName = ticket.PurchaseProofFileName };

        // Tombol aksi Maintenance (aturan sama dengan TicketFlow).
        if (ticket.TicketType == (byte)TicketTypeEnum.Maintenance && ticket.ClosedAt == null)
            result = MaintenanceActionFlags.Apply(result, canAct, currentStep);

        return result;
    }

    private async Task<int?> GetStepLevelAsync(byte ticketType, string stepCode, CancellationToken ct)
    {
        if (stepCode == StepCodes.Created)
            return StepLevels.Requester;

        return await uow.WorkflowSteps.QueryNoTracking()
            .Where(s => s.StepCode == stepCode
                     && s.Workflow.TicketType == ticketType
                     && s.Workflow.IsActive)
            .Select(s => s.Level)
            .FirstOrDefaultAsync(ct);
    }
}