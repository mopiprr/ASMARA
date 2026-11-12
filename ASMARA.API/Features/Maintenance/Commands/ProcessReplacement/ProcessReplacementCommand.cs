
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Features.Maintenance.Common;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.ProcessReplacement;

public record ProcessReplacementCommand(
    Guid TicketId,
    ReplacementRequestDto Request
) : IRequest<TransitionResult>;

public class ProcessReplacementCommandHandler(
    IUnitOfWork uow,
    ITicketFlowService ticketFlow,
    ICurrentUser currentUser,
    IMediator mediator
) : IRequestHandler<ProcessReplacementCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(
        ProcessReplacementCommand command,
        CancellationToken ct)
    {
        var request = command.Request;

        var (ticket, result) = await uow.ExecuteInTransactionAsync(async ct =>
        {
            var ticket = await uow.Tickets.Query()
                .Include(t => t.Employee)
                .Include(t => t.MaintenanceDetail)
                .FirstOrDefaultAsync(
                    t => t.Id == command.TicketId &&
                         t.TicketType == (byte)TicketTypeEnum.Maintenance,
                    ct);

            if (ticket is null)
                throw new NotFoundException(
                    $"Maintenance ticket {command.TicketId} was not found.");

            var detail = ticket.MaintenanceDetail
                ?? throw new BusinessRuleException(
                    "Maintenance detail was not found.");

            var currentStep = await uow.Tickets.GetCurrentStepAsync(
                ticket.Id, ct);

            if (currentStep != StepCodes.InventoryDispose &&
                currentStep != StepCodes.InventoryArchive)
            {
                throw new TicketStateException(
                    "Replacement can only be processed at INVENTORY_DISPOSE or INVENTORY_ARCHIVE.");
            }

            TransitionResult result;

            if (request.Available)
            {
                if (!request.ReplacementAssetId.HasValue)
                {
                    throw new BusinessRuleException(
                        "Please select a replacement asset.");
                }

                result = await ticketFlow.ExecuteAsync(
                    ticket.Id,
                    TicketActionEnum.ReplacementAvailable,
                    request.Note,
                    ct);

                await RetireOldAssetAsync(
                    detail.AssetId,
                    result.FromStep,
                    ct);

                await AssignReplacementAsync(
                    detail,
                    request.ReplacementAssetId.Value,
                    ct);
            }
            else
            {
                await EnsureNoActiveProcurementChildAsync(ticket.Id, ct);

                var child = await ChildProcurementBuilder.BuildAsync(
                    uow.Tickets,
                    uow.ProcurementDetails,
                    uow.ItemInventories,
                    uow.ItemCategories,
                    request.Subject!,
                    request.Description,
                    request.Items!,
                    ct);

                var childResult = await ticketFlow.CreateChildAsync(
                    ticket.Id,
                    TicketActionEnum.ReplacementUnavailable,
                    child,
                    request.Note,
                    ct);

                result = childResult.Parent;

                await RetireOldAssetAsync(
                    detail.AssetId,
                    result.FromStep,
                    ct);
            }

            await uow.SaveChangesAsync(ct);

            return (ticket, result);
        }, ct);

        var actor = await uow.Employees.GetByIdAsync(
            currentUser.EmployeeId, ct) ?? ticket.Employee;

        var action = request.Available
            ? TicketActionEnum.ReplacementAvailable
            : TicketActionEnum.ReplacementUnavailable;

        await mediator.Publish(
            new TicketStatusChangedEvent(
                ticket,
                ticket.Employee,
                actor,
                action.ToString(),
                request.Note),
            ct);

        return result;
    }

    private async Task EnsureNoActiveProcurementChildAsync(
        Guid parentTicketId,
        CancellationToken ct)
    {
        var childIds = await uow.Tickets.Query()
            .Where(t =>
                t.ParentTicketId == parentTicketId &&
                t.TicketType == (byte)TicketTypeEnum.Procurement)
            .Select(t => t.Id)
            .ToListAsync(ct);

        foreach (var childId in childIds)
        {
            var childStep = await uow.Tickets.GetCurrentStepAsync(
                childId, ct);

            // Procurement yang sudah ditolak atau dibatalkan
            // boleh dibuat ulang jika diperlukan.
            if (childStep != StepCodes.Rejected &&
                childStep != StepCodes.Cancelled)
            {
                throw new BusinessRuleException(
                    "A Procurement ticket already exists for this maintenance ticket.");
            }
        }
    }

    private async Task RetireOldAssetAsync(
        int assetId,
        string fromStep,
        CancellationToken ct)
    {
        var status = fromStep switch
        {
            StepCodes.InventoryDispose => AssetStatusEnum.Disposed,
            StepCodes.InventoryArchive => AssetStatusEnum.Archived,
            _ => throw new TicketStateException(
                $"Cannot determine retirement action from step {fromStep}.")
        };

        if (!await uow.Assets.RetireAsync(assetId, status, ct))
        {
            throw new BusinessRuleException(
                $"Asset {assetId} is not under maintenance.");
        }
    }

    private async Task AssignReplacementAsync(
        MaintenanceDetail detail,
        int replacementAssetId,
        CancellationToken ct)
    {
        var oldOwnerId = await uow.Assets.QueryNoTracking()
            .Where(a => a.Id == detail.AssetId)
            .Select(a => a.EmployeeId)
            .FirstOrDefaultAsync(ct);

        if (!await uow.Assets.TryAssignReplacementAsync(
            replacementAssetId,
            oldOwnerId,
            ct))
        {
            throw new BusinessRuleException(
                $"Asset {replacementAssetId} is not available as a replacement.");
        }

        detail.ReplacementAssetId = replacementAssetId;
    }
}
