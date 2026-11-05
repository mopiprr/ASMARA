
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
    ICurrentUser currentUser)
    : IRequestHandler<GetProcurementTicketsQuery, List<TicketResponseDto>>
{
    public async Task<List<TicketResponseDto>> Handle(
        GetProcurementTicketsQuery query,
        CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;

        var isFinance = currentUser.IsInRole(Roles.Finance);
        var isPurchasing = currentUser.IsInRole(Roles.Purchasing);

        var procurementQuery = tickets.QueryNoTracking()
            .Where(t =>
                t.TicketType == (byte)TicketTypeEnum.Procurement);

        procurementQuery = procurementQuery.Where(t =>
            // Requester: hanya melihat tiket miliknya.
            t.EmployeeId == employeeId

            // Approver individual: hanya melihat tiket yang ditugaskan.
            || t.CurrentTaskEmployeeId == employeeId

            // Finance: melihat tiket pada tahap Finance Approval.
            || (isFinance &&
                t.TicketWorkflowLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .ThenByDescending(log => log.Id)
                    .Select(log => log.ToStep)
                    .FirstOrDefault() == StepCodes.FinanceApproval)

            // Purchasing: melihat tiket pada tahap Purchasing.
            || (isPurchasing &&
                t.TicketWorkflowLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .ThenByDescending(log => log.Id)
                    .Select(log => log.ToStep)
                    .FirstOrDefault() == StepCodes.Purchasing)
        );

        return await procurementQuery
            .OrderByDescending(t => t.CreatedAt)
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

                t.TicketWorkflowLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .ThenByDescending(log => log.Id)
                    .Select(log => log.ToStep)
                    .FirstOrDefault() ?? StepCodes.Created,

                null,

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
                    .ToList()
            ))
            .ToListAsync(ct);
    }
}
