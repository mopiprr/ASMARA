using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;

namespace ASMARA.API.Features.Maintenance.Commands.CreateMaintenanceTicket;

public record CreateMaintenanceTicketCommand(CreateMaintenanceTicketRequestDto Request) : IRequest<Guid>;

public class CreateMaintenanceTicketCommandHandler(IUnitOfWork uow, ITicketFlowService ticketFlow, ICurrentUser currentUser, IMediator mediator)
    : IRequestHandler<CreateMaintenanceTicketCommand, Guid>
{
    public async Task<Guid> Handle(CreateMaintenanceTicketCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var requesterId = currentUser.EmployeeId;

        if (!await uow.Assets.TryClaimForMaintenanceAsync(request.AssetId, requesterId, ct))
            throw new BusinessRuleException("Asset cannot be submitted: not found, not yours, or not currently InUse.");

        var ticketNumber = await uow.Tickets.GenerateTicketNumberAsync(TicketTypeEnum.Maintenance, ct);

        var now = DateTime.UtcNow;

        var (ticket, maintenanceDetail, flowResult) = await uow.ExecuteInTransactionAsync(async ct =>
        {
            var ticket = new Ticket
            {
                Id = Guid.CreateVersion7(),
                EmployeeId = requesterId,
                TicketNumber = ticketNumber,
                TicketType = (byte)TicketTypeEnum.Maintenance,
                Subject = request.Subject.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                CreatedAt = now
            };
            uow.Tickets.Add(ticket);

            var detail = new MaintenanceDetail
            {
                TicketId = ticket.Id,
                AssetId = request.AssetId,
                Description = request.ProblemDescription.Trim(),
                CreatedAt = now
            };
            uow.MaintenanceDetails.Add(detail);

            var logNote = !string.IsNullOrWhiteSpace(request.ProblemDescription)
                ? request.ProblemDescription.Trim()
                : request.Subject.Trim();

            var result = await ticketFlow.ExecuteAsync(ticket.Id, TicketActionEnum.Create, note: logNote, ct);

            await uow.SaveChangesAsync(ct);
            
            return (ticket, detail, result);
        }, ct);

        var requester = await uow.Employees.GetByIdAsync(ticket.EmployeeId, ct);
        Employee? manager = null;
        if (flowResult.NextTaskEmployeeId.HasValue)
        {
            manager = await uow.Employees.GetByIdAsync(flowResult.NextTaskEmployeeId.Value, ct);
        }
        else if (requester?.ManagerId is not null)
        {
            manager = await uow.Employees.GetByIdAsync(requester.ManagerId.Value, ct);
        }

        if (requester is not null)
        {
            await mediator.Publish(new TicketCreatedEvent(
                ticket,
                requester,
                manager,
                ProcurementItems: null,
                MaintenanceDetail: maintenanceDetail), ct);
        }

        return ticket.Id;
    }
}
