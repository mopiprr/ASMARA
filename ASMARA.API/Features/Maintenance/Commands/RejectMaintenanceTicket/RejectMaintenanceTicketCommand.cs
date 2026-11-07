using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.RejectMaintenanceTicket;

public record RejectMaintenanceTicketCommand(Guid TicketId, string? Note) : IRequest<TransitionResult>;

public class RejectMaintenanceTicketCommandHandler(IUnitOfWork uow, ITicketFlowService ticketFlow, ICurrentUser currentUser, IMediator mediator)
    : IRequestHandler<RejectMaintenanceTicketCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(RejectMaintenanceTicketCommand command, CancellationToken ct)
    {
        var (ticket, result) = await uow.ExecuteInTransactionAsync(async ct =>
        {
            var ticket = await uow.Tickets.Query()
                .Include(t => t.Employee)
                .Include(t => t.MaintenanceDetail)
                .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == (byte)TicketTypeEnum.Maintenance, ct);
            if (ticket == null)
            {
                throw new NotFoundException($"Maintenance ticket {command.TicketId} was not found.");
            }

            var result = await ticketFlow.ExecuteAsync(ticket.Id, TicketActionEnum.Reject, command.Note, ct);

            var assetId = ticket.MaintenanceDetail!.AssetId;
            if (!await uow.Assets.ReleaseFromMaintenanceAsync(assetId, ct))
                throw new BusinessRuleException($"Asset {assetId} is not under maintenance.");

            await uow.SaveChangesAsync(ct);

            return (ticket, result);
        }, ct);

        var actor = await uow.Employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;

        await mediator.Publish(new TicketStatusChangedEvent(
            ticket,
            ticket.Employee,
            actor,
            TicketActionEnum.Reject.ToString(),
            command.Note), ct);

        return result;
    }
}