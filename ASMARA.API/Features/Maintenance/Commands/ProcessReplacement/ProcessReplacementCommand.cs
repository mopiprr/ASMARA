using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.ProcessReplacement;

public record ProcessReplacementCommand(Guid TicketId, ReplacementRequestDto Request) : IRequest<TransitionResult>;

public class ProcessReplacementCommandHandler(IUnitOfWork uow, ITicketRepository tickets, IAssetRepository assets, ITicketFlowService ticketFlow,
    IEmployeeRepository employees, ICurrentUser currentUser, IMediator mediator)
    : IRequestHandler<ProcessReplacementCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(ProcessReplacementCommand command, CancellationToken ct)
    {
        var request = command.Request;

        if (!request.Available)
            throw new BusinessRuleException("Replacement without stock requires a child procurement ticket (not available yet).");

        var (ticket, result) = await uow.ExecuteInTransactionAsync(async ct =>
        {
            var ticket = await tickets.Query()
                .Include(t => t.Employee)
                .Include(t => t.MaintenanceDetail)
                .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == (byte)TicketTypeEnum.Maintenance, ct);
            if (ticket == null)
                throw new NotFoundException($"Maintenance ticket {command.TicketId} not found.");


            var detail = ticket.MaintenanceDetail!;

            var result = await ticketFlow.ExecuteAsync(ticket.Id, TicketActionEnum.ReplacementAvailable, request.Note, ct);

            var retiredStatus = result.FromStep == StepCodes.InventoryDispose
                ? AssetStatusEnum.Disposed
                : AssetStatusEnum.Archived;

            if (!await assets.RetireAsync(detail.AssetId, retiredStatus, ct))
                throw new BusinessRuleException($"Asset {detail.AssetId} is not under maintenance.");

            var oldOwnerId = await assets.QueryNoTracking()
                .Where(a => a.Id == detail.AssetId)
                .Select(a => a.EmployeeId)
                .FirstOrDefaultAsync(ct);

            if (!await assets.TryAssignReplacementAsync(request.ReplacementAssetId!.Value, oldOwnerId, ct))
                throw new BusinessRuleException(
                    $"Asset {request.ReplacementAssetId} is not available as a replacement.");

            detail.ReplacementAssetId = request.ReplacementAssetId;

            await uow.SaveChangesAsync(ct);
            return (ticket, result);
        }, ct);

        var actor = await employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;
        await mediator.Publish(new TicketStatusChangedEvent(
            ticket, 
            ticket.Employee, 
            actor, 
            TicketActionEnum.ReplacementAvailable.ToString(), 
            request.Note), ct);

        return result;
    }
}