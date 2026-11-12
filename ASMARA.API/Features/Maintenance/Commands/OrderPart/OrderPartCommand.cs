
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Maintenance.Common;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.OrderPart;

public record OrderPartCommand(
    Guid TicketId,
    OrderPartRequestDto Request
) : IRequest<ChildTicketResult>;

public class OrderPartCommandHandler(
    IUnitOfWork uow,
    ITicketFlowService ticketFlow,
    ICurrentUser currentUser,
    IMediator mediator
) : IRequestHandler<OrderPartCommand, ChildTicketResult>
{
    public async Task<ChildTicketResult> Handle(
        OrderPartCommand command,
        CancellationToken ct)
    {
        var request = command.Request;

        var transactionResult = await uow.ExecuteInTransactionAsync(
            async txCt =>
            {
                var ticket = await uow.Tickets.Query()
                    .Include(t => t.Employee)
                    .Include(t => t.MaintenanceDetail)
                    .FirstOrDefaultAsync(
                        t => t.Id == command.TicketId &&
                             t.TicketType == (byte)TicketTypeEnum.Maintenance,
                        txCt);

                if (ticket == null)
                {
                    throw new NotFoundException(
                        $"Maintenance ticket {command.TicketId} was not found.");
                }

                if (request.Items == null || request.Items.Count == 0)
                {
                    throw new FluentValidation.ValidationException(
                        "At least one procurement item is required.");
                }

                var procurementItems = request.Items
                    .Select(item => new CreateProcurementItemRequest(
                        ItemCategoryId: item.ItemCategoryId,
                        ItemName: item.ItemName,
                        Quantity: item.Quantity,
                        Price: item.Price,
                        ItemId: item.ItemId
                    ))
                    .ToList();

                var child = await ChildProcurementBuilder.BuildAsync(
                    uow.Tickets,
                    uow.ProcurementDetails,
                    uow.ItemInventories,
                    uow.ItemCategories,
                    request.Subject,
                    request.Description,
                    procurementItems,
                    txCt
                );

                var childResult = await ticketFlow.CreateChildAsync(
                    ticket.Id,
                    TicketActionEnum.OrderPart,
                    child,
                    request.Note,
                    txCt
                );

                await uow.SaveChangesAsync(txCt);

                return (Ticket: ticket, Result: childResult);
            },
            ct
        );

        var ticketResult = transactionResult.Ticket;
        var result = transactionResult.Result;

        var actor = await uow.Employees.GetByIdAsync(
            currentUser.EmployeeId,
            ct
        ) ?? ticketResult.Employee;

        await mediator.Publish(
            new TicketStatusChangedEvent(
                ticketResult,
                ticketResult.Employee,
                actor,
                TicketActionEnum.OrderPart.ToString(),
                request.Note
            ),
            ct
        );

        return result;
    }
}
