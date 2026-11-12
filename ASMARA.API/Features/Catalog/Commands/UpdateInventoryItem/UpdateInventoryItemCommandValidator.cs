using FluentValidation;

namespace ASMARA.API.Features.Catalog.Commands.UpdateInventoryItem;

public class UpdateInventoryItemCommandValidator : AbstractValidator<UpdateInventoryItemCommand>
{
    public UpdateInventoryItemCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Invalid item ID.");

        RuleFor(x => x.ItemName)
            .NotEmpty()
            .WithMessage("Item name is required.")
            .MaximumLength(100)
            .WithMessage("Item name cannot exceed 100 characters.");

        When(x => x.ItemCategoryId.HasValue, () =>
        {
            RuleFor(x => x.ItemCategoryId!.Value)
                .GreaterThan(0)
                .WithMessage("Invalid category ID.");
        });

        When(x => x.NewStock.HasValue, () =>
        {
            RuleFor(x => x.NewStock!.Value)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Stock count cannot be negative.")
                .LessThanOrEqualTo(10000)
                .WithMessage("Stock count cannot exceed 10,000 units.");
        });
    }
}
