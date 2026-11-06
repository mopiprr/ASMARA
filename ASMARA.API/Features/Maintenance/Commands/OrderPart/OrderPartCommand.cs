using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Maintenance.Common;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.OrderPart;

public record OrderPartCommand(Guid TicketId, OrderPartRequestDto Request) : IRequest<ChildTicketResult>;

public class OrderPartCommandHandler(IUnitOfWork uow, ITicketRepository tickets, IProcurementDetailRepository procurementDetails,
    IItemInventoryRepository itemInventories, IItemCategoryRepository itemCategories, ITicketFlowService ticketFlow,
    IEmployeeRepository employees, ICurrentUser currentUser, IMediator mediator)
    : IRequestHandler<OrderPartCommand, ChildTicketResult>
{
    public async Task<ChildTicketResult> Handle(OrderPartCommand command, CancellationToken ct)
    {
        var request = command.Request;

        var (ticket, result) = await uow.ExecuteInTransactionAsync(async ct =>
        {
            var ticket = await tickets.Query()
                .Include(t => t.Employee)
                .FirstOrDefaultAsync(t => t.Id == command.TicketId && t.TicketType == (byte)TicketTypeEnum.Maintenance, ct);
            if (ticket == null)
                throw new NotFoundException($"Maintenance ticket {command.TicketId} was not found.");

            var child = await ChildProcurementBuilder.BuildAsync(
                tickets, procurementDetails, itemInventories, itemCategories,
                request.Subject, request.Description, request.Items, ct);

            var result = await ticketFlow.CreateChildAsync(
                ticket.Id, TicketActionEnum.OrderPart, child, request.Note, ct);

            await uow.SaveChangesAsync(ct);
            return (ticket, result);
        }, ct);

        var actor = await employees.GetByIdAsync(currentUser.EmployeeId, ct) ?? ticket.Employee;
        await mediator.Publish(new TicketStatusChangedEvent(
            ticket, ticket.Employee, actor, TicketActionEnum.OrderPart.ToString(), request.Note), ct);

        return result;
    }
}