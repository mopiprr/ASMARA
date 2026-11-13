using System.Text.Json;
using FluentValidation;
using ASMARA.API.Common.Exceptions;
using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Features.Tickets.Events;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Maintenance.Commands.PartCheck;

public record PartCheckCommand(
    Guid TicketId,
    string? Note
) : IRequest<TransitionResult>;

public class PartCheckCommandHandler(
    IUnitOfWork uow,
    ITicketFlowService ticketFlow,
    ICurrentUser currentUser,
    IMediator mediator
) : IRequestHandler<PartCheckCommand, TransitionResult>
{
    public async Task<TransitionResult> Handle(
        PartCheckCommand command,
        CancellationToken ct)
    {
        var (ticket, result) = await uow.ExecuteInTransactionAsync(async txCt =>
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

            var json = ticket.MaintenanceDetail?.PartRequirement;

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ValidationException(
                    "No spare part requirements were found.");
            }

            List<MaintenancePartItemRequestDto>? requiredParts;

            try
            {
                requiredParts =
                    JsonSerializer.Deserialize<List<MaintenancePartItemRequestDto>>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
            }
            catch (JsonException)
            {
                throw new ValidationException(
                    "Spare part requirement data is invalid or uses an unsupported legacy format.");
            }

            if (requiredParts == null || requiredParts.Count == 0)
            {
                throw new ValidationException(
                    "No spare part requirements were found.");
            }

            // Mengumpulkan kebutuhan stok berdasarkan ItemInventory.
            // Jika satu barang diminta beberapa kali, jumlahnya dijumlahkan.
            var requiredInventory = new Dictionary<int, (ItemInventory Item, int Quantity)>();

            foreach (var part in requiredParts)
            {
                if (part.Quantity <= 0)
                {
                    throw new ValidationException(
                        $"Invalid requested quantity for {part.ItemName}.");
                }

                if (part.ItemCategoryId <= 0 ||
                    string.IsNullOrWhiteSpace(part.ItemName))
                {
                    throw new ValidationException(
                        "Spare part information is incomplete.");
                }

                var category = await uow.ItemCategories.Query()
                    .AnyAsync(
                        c => c.Id == part.ItemCategoryId &&
                             c.IsActive &&
                             c.TrackingType == 1,
                        txCt);

                if (!category)
                {
                    throw new ValidationException(
                        $"Category for {part.ItemName} is not an active consumable category.");
                }

                ItemInventory? inventoryItem;

                if (part.ItemId.HasValue)
                {
                    inventoryItem = await uow.ItemInventories.Query()
                        .FirstOrDefaultAsync(
                            i => i.Id == part.ItemId.Value &&
                                 i.ItemCategoryId == part.ItemCategoryId &&
                                 i.IsActive,
                            txCt);
                }
                else
                {
                    // Barang manual yang baru dibeli dapat ditemukan
                    // berdasarkan kategori dan nama setelah diterima Purchasing.
                    var itemName = part.ItemName.Trim().ToLower();

                    inventoryItem = await uow.ItemInventories.Query()
                        .FirstOrDefaultAsync(
                            i => i.ItemCategoryId == part.ItemCategoryId &&
                                 i.ItemName.ToLower() == itemName &&
                                 i.IsActive,
                            txCt);
                }

                if (inventoryItem == null)
                {
                    throw new ValidationException(
                        $"Inventory item '{part.ItemName}' was not found. Please ensure the purchased item has been received into Inventory.");
                }

                if (requiredInventory.TryGetValue(
                    inventoryItem.Id,
                    out var existing))
                {
                    requiredInventory[inventoryItem.Id] = (
                        existing.Item,
                        existing.Quantity + part.Quantity
                    );
                }
                else
                {
                    requiredInventory.Add(
                        inventoryItem.Id,
                        (inventoryItem, part.Quantity)
                    );
                }
            }

            // Validasi seluruh stok terlebih dahulu.
            // Tidak ada stok yang dikurangi sebelum semua barang mencukupi.
            foreach (var entry in requiredInventory.Values)
            {
                var available = entry.Item.Qty ?? 0;

                if (available < entry.Quantity)
                {
                    throw new ValidationException(
                        $"Insufficient stock for '{entry.Item.ItemName}'. " +
                        $"Required: {entry.Quantity}, Available: {available}.");
                }
            }

            // Semua stok sudah mencukupi, baru lakukan pengurangan.
            foreach (var entry in requiredInventory.Values)
            {
                entry.Item.Qty =
                    (entry.Item.Qty ?? 0) - entry.Quantity;
            }

            // Pindahkan workflow ke Handler Receive Part.
            // Memakai aksi PartAvailable yang sudah ada di TransitionMap
            // (INVENTORY_PART_CHECK -> HANDLER_RECEIVE_PART), jadi TicketFlow tidak perlu diubah.
            var result = await ticketFlow.ExecuteAsync(
                ticket.Id,
                TicketActionEnum.PartAvailable,
                command.Note,
                txCt);

            await uow.SaveChangesAsync(txCt);

            return (ticket, result);
        }, ct);

        var actor = await uow.Employees.GetByIdAsync(
            currentUser.EmployeeId,
            ct) ?? ticket.Employee;

        await mediator.Publish(
            new TicketStatusChangedEvent(
                ticket,
                ticket.Employee,
                actor,
                TicketActionEnum.PartAvailable.ToString(),
                command.Note),
            ct);

        return result;
    }
}

public class PartCheckCommandValidator : AbstractValidator<PartCheckCommand>
{
    public PartCheckCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty();

        RuleFor(x => x.Note)
            .MaximumLength(200);
    }
}