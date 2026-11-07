using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.ReceiveItem;

public record ReceiveItemCommand(Guid TicketId, string? Note) : IRequest<TransitionResult>;

public class ReceiveItemCommandHandler(IUnitOfWork uow, ITicketFlowService ticketFlow, ICurrentUser currentUser, IMediator mediator)
    : IRequestHandler<ReceiveItemCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(ReceiveItemCommand command, CancellationToken ct)
    {
        var (ticket, result) = await uow.ExecuteInTransactionAsync(async ct =>
        {
            var ticket = await uow.Tickets.Query()
                .Include(t => t.Employee)
                .Include(t => t.MaintenanceDetail)
                .FirstOrDefaultAsync(t => t.Id == command.TicketId
                                       && t.TicketType == (byte)TicketTypeEnum.Maintenance, ct);
            if (ticket == null)
                throw new NotFoundException($"Maintenance ticket {command.TicketId} was not found.");

            var result = await ticketFlow.ExecuteAsync(ticket.Id, TicketActionEnum.Receive, command.Note, ct);

            var detail = ticket.MaintenanceDetail!;
            if (result.FromStep == StepCodes.RequesterReceiveItem && detail.ReplacementAssetId is null)
            {
                if (!await uow.Assets.ReleaseFromMaintenanceAsync(detail.AssetId, ct))
                    throw new BusinessRuleException($"Asset {detail.AssetId} is not under maintenance.");
            }

            await uow.SaveChangesAsync(ct);
            return (ticket, result);
        }, ct);

        var actor = await uow.Employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;
        await mediator.Publish(new TicketStatusChangedEvent(
            ticket, 
            ticket.Employee, 
            actor, 
            TicketActionEnum.Receive.ToString(), 
            command.Note), ct);

        return result;
    }
}