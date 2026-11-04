using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;

namespace ASMARA.API.Features.Maintenance.Commands.CreateMaintenanceTicket;

public record CreateMaintenanceTicketCommand(CreateMaintenanceTicketRequestDto Request) : IRequest<Guid>;

public class CreateMaintenanceTicketCommandHandler(IUnitOfWork uow, ITicketRepository tickets, IMaintenanceDetailRepository maintenanceDetails,
    IAssetRepository assets, ITicketFlowService ticketFlow, ICurrentUser currentUser)
    : IRequestHandler<CreateMaintenanceTicketCommand, Guid>
{
    public Task<Guid> Handle(CreateMaintenanceTicketCommand command, CancellationToken ct)
        => uow.ExecuteInTransactionAsync(async ct =>
        {
            var request = command.Request;
            var requesterId = currentUser.EmployeeId;

            if (!await assets.TryClaimForMaintenanceAsync(request.AssetId, requesterId, ct))
                throw new BusinessRuleException("Asset cannot be submitted: not found, not yours, or not currently InUse.");

            var ticketNumber = await tickets.GenerateTicketNumberAsync(TicketTypeEnum.Maintenance, ct);

            var now = DateTime.UtcNow;
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
            tickets.Add(ticket);

            maintenanceDetails.Add(new MaintenanceDetail
            {
                TicketId = ticket.Id,
                AssetId = request.AssetId,
                Description = request.ProblemDescription.Trim(),
                CreatedAt = now
            });

            await ticketFlow.ExecuteAsync(ticket.Id, TicketActionEnum.Create, note: null, ct);

            await uow.SaveChangesAsync(ct);



            return ticket.Id;
        }, ct);
}