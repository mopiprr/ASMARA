using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.OrderPart;

public class OrderPartCommandValidator : AbstractValidator<OrderPartCommand>
{
    public OrderPartCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("Ticket ID is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        RuleFor(x => x.Request.Subject)
            .NotEmpty().WithMessage("Request Subject is required.")
            .MaximumLength(50).WithMessage("Request Subject must not exceed 50 characters.");

        RuleFor(x => x.Request.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.Request.Note)
            .MaximumLength(200).WithMessage("Note must not exceed 200 characters.");

        RuleFor(x => x.Request.Items)
            .NotEmpty().WithMessage("Please add at least one part to order.");

        RuleForEach(x => x.Request.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than 0.");

            item.RuleFor(i => i.Price)
                .GreaterThan(0).WithMessage("Estimated price must be greater than 0.");

            item.When(i => i.ItemId is null, () =>
            {
                item.RuleFor(i => i.ItemName)
                    .NotEmpty().WithMessage("Item name is required for a manual item.")
                    .MaximumLength(50).WithMessage("Item name must not exceed 50 characters.");

                item.RuleFor(i => i.ItemCategoryId)
                    .GreaterThan(0).WithMessage("Please select an item category for a manual item.");
            });
        });
    }
}