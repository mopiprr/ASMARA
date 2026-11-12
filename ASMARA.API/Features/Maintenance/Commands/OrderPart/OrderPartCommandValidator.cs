
using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.OrderPart;

public class OrderPartCommandValidator
    : AbstractValidator<OrderPartCommand>
{
    public OrderPartCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty()
            .WithMessage("Maintenance ticket ID is required.");

        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Procurement request is required.");

        RuleFor(x => x.Request.Subject)
            .NotEmpty()
            .WithMessage("Procurement subject is required.")
            .MaximumLength(200);

        RuleFor(x => x.Request.Items)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one procurement item is required.");

        RuleForEach(x => x.Request.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.ItemCategoryId)
                    .GreaterThan(0)
                    .WithMessage("Item category is required.");

                item.RuleFor(x => x.ItemName)
                    .NotEmpty()
                    .WithMessage("Item name is required.");

                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0)
                    .WithMessage("Quantity must be greater than zero.");

                item.RuleFor(x => x.Price)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Price cannot be negative.");

                item.RuleFor(x => x.ItemId)
                    .GreaterThan(0)
                    .When(x => x.ItemId.HasValue)
                    .WithMessage("Invalid inventory item ID.");
            });
    }
}
