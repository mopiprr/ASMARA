using System.Text.Json;
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.SubmitRepairResult;

public record SubmitRepairResultCommand(
    Guid TicketId,
    SubmitRepairResultRequestDto Request
) : IRequest<TransitionResult>;

public class SubmitRepairResultCommandHandler(
    IUnitOfWork uow,
    ITicketFlowService ticketFlow,
    ICurrentUser currentUser,
    IMediator mediator
) : IRequestHandler<SubmitRepairResultCommand, TransitionResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<TransitionResult> Handle(
        SubmitRepairResultCommand command,
        CancellationToken ct)
    {
        var request = command.Request;

        var ticket = await uow.Tickets.Query()
            .Include(t => t.Employee)
            .Include(t => t.MaintenanceDetail)
            .FirstOrDefaultAsync(
                t => t.Id == command.TicketId
                     && t.TicketType == (byte)TicketTypeEnum.Maintenance,
                ct);

        if (ticket is null)
        {
            throw new NotFoundException(
                $"Maintenance ticket {command.TicketId} was not found.");
        }

        var detail = ticket.MaintenanceDetail
            ?? throw new BusinessRuleException(
                "Maintenance detail was not found.");

        var handlerNote = string.IsNullOrWhiteSpace(request.HandlerNote)
            ? null
            : request.HandlerNote.Trim();

        TicketActionEnum action;

        switch (request.Result)
        {
            case RepairResultEnum.Repaired:

                action = TicketActionEnum.Repaired;

                detail.RepairResult = (byte)RepairResultEnum.Repaired;
                detail.HandlerNote = handlerNote;
                detail.PartRequirement = null;

                break;

            case RepairResultEnum.Unrepairable:

                action = TicketActionEnum.Unrepairable;

                detail.RepairResult = (byte)RepairResultEnum.Unrepairable;
                detail.HandlerNote = handlerNote;
                detail.PartRequirement = null;

                break;

            case RepairResultEnum.NeedPart:

                action = TicketActionEnum.NeedPart;

                detail.RepairResult = (byte)RepairResultEnum.NeedPart;
                detail.HandlerNote = handlerNote;

                if (request.Parts is { Count: > 0 })
                {
                    var normalizedParts =
                        new List<MaintenancePartItemRequestDto>();

                    foreach (var part in request.Parts)
                    {
                        if (string.IsNullOrWhiteSpace(part.ItemName))
                        {
                            throw new BusinessRuleException(
                                "Spare part name is required.");
                        }

                        if (part.Quantity <= 0)
                        {
                            throw new BusinessRuleException(
                                $"Quantity for '{part.ItemName}' must be greater than zero.");
                        }

                        string itemName;
                        int categoryId;

                        if (part.ItemId.HasValue)
                        {
                            var inventoryItem = await uow.ItemInventories
                                .Query()
                                .Where(i =>
                                    i.Id == part.ItemId.Value &&
                                    i.IsActive)
                                .Select(i => new
                                {
                                    i.ItemName,
                                    i.ItemCategoryId
                                })
                                .FirstOrDefaultAsync(ct);

                            if (inventoryItem is null)
                            {
                                throw new BusinessRuleException(
                                    $"Inventory item {part.ItemId.Value} was not found or inactive.");
                            }

                            itemName = inventoryItem.ItemName;
                            categoryId = inventoryItem.ItemCategoryId;
                        }
                        else
                        {
                            var categoryExists = await uow.ItemCategories
                                .Query()
                                .AnyAsync(
                                    c => c.Id == part.ItemCategoryId &&
                                         c.IsActive,
                                    ct);

                            if (!categoryExists)
                            {
                                throw new BusinessRuleException(
                                    $"Item category {part.ItemCategoryId} was not found or inactive.");
                            }

                            itemName = part.ItemName.Trim();
                            categoryId = part.ItemCategoryId;
                        }

                        normalizedParts.Add(
                            new MaintenancePartItemRequestDto(
                                part.ItemId,
                                categoryId,
                                itemName,
                                part.Quantity
                            )
                        );
                    }

                    detail.PartRequirement = JsonSerializer.Serialize(
                        normalizedParts,
                        JsonOptions
                    );
                }
                else if (!string.IsNullOrWhiteSpace(request.PartRequirement))
                {
                    // Kompatibilitas dengan request frontend versi lama.
                    // Nilai lama tetap disimpan sebagai teks biasa.
                    detail.PartRequirement = request.PartRequirement.Trim();
                }
                else
                {
                    throw new BusinessRuleException(
                        "At least one spare part is required when repair result is NeedPart.");
                }

                break;

            default:

                throw new BusinessRuleException(
                    "Invalid repair result. Allowed values are Repaired, Unrepairable, and NeedPart.");
        }

        var result = await ticketFlow.ExecuteAsync(
            ticket.Id,
            action,
            note: handlerNote,
            ct);

        await uow.SaveChangesAsync(ct);

        var actor = await uow.Employees.GetByIdAsync(
            currentUser.EmployeeId,
            ct) ?? ticket.Employee;

        await mediator.Publish(
            new TicketStatusChangedEvent(
                ticket,
                ticket.Employee,
                actor,
                action.ToString(),
                handlerNote),
            ct);

        return result;
    }
}