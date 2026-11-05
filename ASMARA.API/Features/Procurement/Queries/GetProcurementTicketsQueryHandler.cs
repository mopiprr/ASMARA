
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Procurement.Queries.GetProcurementTickets;

public class GetProcurementTicketsQueryHandler(
    ITicketRepository tickets,
    IWorkflowStepRepository workflowSteps,
    IEmployeeRepository employees,
    ICurrentUser currentUser)
    : IRequestHandler<GetProcurementTicketsQuery, List<TicketResponseDto>>
{
    public async Task<List<TicketResponseDto>> Handle(
        GetProcurementTicketsQuery query,
        CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;
        var roles = currentUser.Roles;

        var procurementTickets = await tickets.QueryNoTracking()
        .Where(t =>
            t.TicketType == (byte)TicketTypeEnum.Procurement &&
            t.EmployeeId == employeeId)
        .Include(t => t.Employee)
        .Include(t => t.TicketWorkflowLogs)
        .Include(t => t.ProcurementDetails)
            .ThenInclude(d => d.Item)
        .Include(t => t.ProcurementDetails)
            .ThenInclude(d => d.ItemCategory)
        .OrderByDescending(t => t.CreatedAt)
        .ToListAsync(ct);

        var result = new List<TicketResponseDto>();

        foreach (var ticket in procurementTickets)
        {
            var currentStep = ticket.TicketWorkflowLogs
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .Select(log => log.ToStep)
                .FirstOrDefault() ?? StepCodes.Created;

            var stepLevel = await GetStepLevelAsync(
                ticket.TicketType,
                currentStep,
                ct);

            var isRequester = ticket.EmployeeId == employeeId;

            var canApprove = false;
            var canRevise = false;
            var canReject = false;
            var canReceiveGoods = false;

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

            var canView =
            isRequester ||
            ticket.CurrentTaskEmployeeId == employeeId ||
            canApprove ||
            canRevise ||
            canReject ||
            canReceiveGoods;

            if (!canView)
                continue;

            var history = ticket.TicketWorkflowLogs
                .OrderBy(log => log.CreatedAt)
                .ThenBy(log => log.Id)
                .Select(log => new TicketWorkflowHistoryDto(
                    log.FromStep,
                    log.ToStep,
                    log.EmployeeId,
                    log.EmployeeName,
                    log.Note,
                    log.CreatedAt
                ))
                .ToList();

            var procurementDetails = ticket.ProcurementDetails
                .Select(detail => new ProcurementDetailResponseDto(
                    detail.TicketId,
                    detail.ItemId,
                    detail.Item != null
                        ? detail.Item.ItemCode
                        : null,
                    detail.ItemCategoryId,
                    detail.ItemCategory.CategoryName,
                    detail.ItemName,
                    detail.Quantity,
                    detail.Price,
                    detail.Quantity * detail.Price,
                    detail.CreatedAt
                ))
                .ToList();

            result.Add(new TicketResponseDto(
                ticket.Id,
                ticket.ParentTicketId,
                ticket.EmployeeId,
                ticket.Employee.FullName,
                ticket.TicketNumber,
                ticket.TicketType,
                ticket.Subject,
                ticket.Description,
                ticket.AssignedToEmployeeId,
                ticket.AssignedTo,
                ticket.CreatedAt,
                ticket.ClosedAt,
                ticket.UpdatedAt,
                ticket.CurrentTaskEmployeeId,
                currentStep,
                null,
                procurementDetails,
                history,
                canApprove,
                canRevise,
                canReject,
                canReceiveGoods
            ));
        }

        return result;
    }

    private async Task<int?> GetStepLevelAsync(
        byte ticketType,
        string stepCode,
        CancellationToken ct)
    {
        if (stepCode == StepCodes.Created)
            return StepLevels.Requester;

        return await workflowSteps.QueryNoTracking()
            .Where(s =>
                s.StepCode == stepCode &&
                s.Workflow.TicketType == ticketType &&
                s.Workflow.IsActive)
            .Select(s => (int?)s.Level)
            .FirstOrDefaultAsync(ct);
    }
}
