using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Queries.GetTicketDetail;

public record GetTicketByIdQuery(Guid Id) : IRequest<TicketResponseDto>;

public class GetTicketByIdQueryHandler(
    ITicketRepository tickets,
    ICurrentUser currentUser)
    : IRequestHandler<GetTicketByIdQuery, TicketResponseDto>
{
    public async Task<TicketResponseDto> Handle(
        GetTicketByIdQuery query,
        CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;

        var dto = await tickets.QueryNoTracking()
            .Where(t =>
                t.Id == query.Id &&
                (
                    t.EmployeeId == employeeId ||
                    t.CurrentTaskEmployeeId == employeeId
                ))
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
                    .ToList()))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                $"Ticket {query.Id} was not found or you are not allowed to access it.");

        var currentStep =
            await tickets.GetCurrentStepAsync(query.Id, ct)
            ?? StepCodes.Created;

        return dto with { CurrentStep = currentStep };
    }
}