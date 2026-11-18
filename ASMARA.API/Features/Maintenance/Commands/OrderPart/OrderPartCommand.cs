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

namespace ASMARA.API.Features.Maintenance.Commands.OrderPart;

public record OrderPartCommand(Guid TicketId, OrderPartRequestDto Request) : IRequest<ChildTicketResult>;

public class OrderPartCommandHandler(IUnitOfWork uow, ITicketFlowService ticketFlow, ICurrentUser currentUser, IMediator mediator)
    : IRequestHandler<OrderPartCommand, ChildTicketResult>
{
    public async Task<ChildTicketResult> Handle(OrderPartCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var (ticket, result, child) = await uow.ExecuteInTransactionAsync(async ct =>
        {
            var ticket = await uow.Tickets.Query()
                .Include(t => t.Employee)
                .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == (byte)TicketTypeEnum.Maintenance, ct);
            if (ticket == null)
                throw new NotFoundException($"Maintenance ticket {command.TicketId} was not found.");

            var child = await ChildProcurementBuilder.BuildAsync(
                uow.Tickets, uow.ProcurementDetails, uow.ItemInventories, uow.ItemCategories,
                request.Subject, request.Description, request.Items, ct);

            var result = await ticketFlow.CreateChildAsync(
                ticket.Id, TicketActionEnum.OrderPart, child, request.Note, ct);

            await uow.SaveChangesAsync(ct);
            return (ticket, result, child);
        }, ct);

        var actor = await uow.Employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;
        Employee? childApprover = null;
        if (result.Child.NextTaskEmployeeId.HasValue)
        {
            childApprover = await uow.Employees.GetByIdAsync(result.Child.NextTaskEmployeeId.Value, ct);
        }
        else if (actor.ManagerId.HasValue)
        {
            childApprover = await uow.Employees.GetByIdAsync(actor.ManagerId.Value, ct);
        }

        await mediator.Publish(new TicketCreatedEvent(child, actor, childApprover), ct);

        await mediator.Publish(new TicketStatusChangedEvent(
            ticket,
            ticket.Employee,
            actor,
            TicketActionEnum.OrderPart.ToString(),
            request.Note), ct);

        return result;
    }
}