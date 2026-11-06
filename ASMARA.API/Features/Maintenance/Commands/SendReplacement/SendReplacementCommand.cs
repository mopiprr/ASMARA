using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.SendReplacement;

public record SendReplacementCommand(Guid TicketId, SendReplacementRequestDto Request) : IRequest<TransitionResult>;

public class SendReplacementCommandHandler(
    IUnitOfWork uow,
    ITicketRepository tickets,
    IAssetRepository assets,
    ITicketFlowService ticketFlow,
    IEmployeeRepository employees,
    ICurrentUser currentUser,
    IMediator mediator)
    : IRequestHandler<SendReplacementCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(SendReplacementCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var (ticket, result) = await uow.ExecuteInTransactionAsync(async ct =>
        {
            var ticket = await tickets.Query()
                .Include(t => t.Employee)
                .Include(t => t.MaintenanceDetail)
                .FirstOrDefaultAsync(t => t.Id == command.TicketId
                                       && t.TicketType == (byte)TicketTypeEnum.Maintenance, ct)
                ?? throw new NotFoundException($"Maintenance ticket {command.TicketId} was not found.");

            var detail = ticket.MaintenanceDetail!;

            var result = await ticketFlow.ExecuteAsync(ticket.Id, TicketActionEnum.Send, request.Note, ct);

            if (result.FromStep != StepCodes.InventorySendReplacement)
                throw new TicketStateException("send-replacement is only for step INVENTORY_SEND_REPLACEMENT.");

            var oldOwnerId = await assets.QueryNoTracking()
                .Where(a => a.Id == detail.AssetId)
                .Select(a => a.EmployeeId)
                .FirstOrDefaultAsync(ct);

            if (!await assets.TryAssignReplacementAsync(request.ReplacementAssetId, oldOwnerId, ct))
                throw new BusinessRuleException($"Asset {request.ReplacementAssetId} is not available as a replacement.");

            detail.ReplacementAssetId = request.ReplacementAssetId;

            await uow.SaveChangesAsync(ct);
            return (ticket, result);
        }, ct);

        var actor = await employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;
        await mediator.Publish(new TicketStatusChangedEvent(
            ticket, ticket.Employee, actor, TicketActionEnum.Send.ToString(), request.Note), ct);

        return result;
    }
}