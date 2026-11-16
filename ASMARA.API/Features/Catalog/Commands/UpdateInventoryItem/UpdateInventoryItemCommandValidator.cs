using FluentValidation;

namespace ASMARA.API.Features.Catalog.Commands.UpdateInventoryItem;

public class UpdateInventoryItemCommandValidator : AbstractValidator<UpdateInventoryItemCommand>
{
    public UpdateInventoryItemCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Invalid item ID.");

        RuleFor(x => x.ItemName)
            .NotEmpty().WithMessage("Item name is required.")
            .MaximumLength(50).WithMessage("Item name must not exceed 50 characters.");

        When(x => x.ItemCategoryId.HasValue, () =>
        {
            RuleFor(x => x.ItemCategoryId!.Value)
                .GreaterThan(0).WithMessage("Please select a valid item category.");
        });

        When(x => x.NewStock.HasValue, () =>
        {
            RuleFor(x => x.NewStock!.Value)
                .GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative.")
                .LessThanOrEqualTo(10000).WithMessage("Stock cannot exceed 10,000 units.");
        });
    }
}