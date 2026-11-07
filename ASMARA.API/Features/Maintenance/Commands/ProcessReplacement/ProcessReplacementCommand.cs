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

public record ProcessReplacementCommand(Guid TicketId, ReplacementRequestDto Request) : IRequest<TransitionResult>;

public class ProcessReplacementCommandHandler(IUnitOfWork uow, ITicketFlowService ticketFlow, ICurrentUser currentUser, IMediator mediator)
    : IRequestHandler<ProcessReplacementCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(ProcessReplacementCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var (ticket, result) = await uow.ExecuteInTransactionAsync(async ct =>
        {
            var ticket = await uow.Tickets.Query()
                .Include(t => t.Employee)
                .Include(t => t.MaintenanceDetail)
                .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == (byte)TicketTypeEnum.Maintenance, ct);
            if (ticket == null)
                throw new NotFoundException($"Maintenance ticket {command.TicketId} was not found.");

            var detail = ticket.MaintenanceDetail!;
            TransitionResult result;

            if (request.Available)
            {
                result = await ticketFlow.ExecuteAsync(ticket.Id, TicketActionEnum.ReplacementAvailable, request.Note, ct);
                await RetireOldAssetAsync(detail.AssetId, result.FromStep, ct);
                await AssignReplacementAsync(detail, request.ReplacementAssetId!.Value, ct);
            }
            else
            {
                var child = await ChildProcurementBuilder.BuildAsync(
                    uow.Tickets, uow.ProcurementDetails, uow.ItemInventories, uow.ItemCategories,
                    request.Subject!, request.Description, request.Items!, ct);

                var childResult = await ticketFlow.CreateChildAsync(
                    ticket.Id, TicketActionEnum.ReplacementUnavailable, child, request.Note, ct);

                result = childResult.Parent;
                await RetireOldAssetAsync(detail.AssetId, result.FromStep, ct);
            }

            await uow.SaveChangesAsync(ct);
            return (ticket, result);
        }, ct);

        var actor = await uow.Employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;
        var action = request.Available ? TicketActionEnum.ReplacementAvailable : TicketActionEnum.ReplacementUnavailable;
        await mediator.Publish(new TicketStatusChangedEvent(
            ticket, 
            ticket.Employee, 
            actor,
            action.ToString(), 
            request.Note), ct);

        return result;
    }

    private async Task RetireOldAssetAsync(int assetId, string fromStep, CancellationToken ct)
    {
        var status = fromStep == StepCodes.InventoryDispose ? AssetStatusEnum.Disposed : AssetStatusEnum.Archived;
        if (!await uow.Assets.RetireAsync(assetId, status, ct))
            throw new BusinessRuleException($"Asset {assetId} is not under maintenance.");
    }

    private async Task AssignReplacementAsync(ASMARA.API.Entities.MaintenanceDetail detail, int replacementAssetId, CancellationToken ct)
    {
        var oldOwnerId = await uow.Assets.QueryNoTracking()
            .Where(a => a.Id == detail.AssetId)
            .Select(a => a.EmployeeId)
            .FirstOrDefaultAsync(ct);

        if (!await uow.Assets.TryAssignReplacementAsync(replacementAssetId, oldOwnerId, ct))
            throw new BusinessRuleException($"Asset {replacementAssetId} is not available as a replacement.");

        detail.ReplacementAssetId = replacementAssetId;
    }
}