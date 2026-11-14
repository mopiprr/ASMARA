using FluentValidation;

namespace ASMARA.API.Features.Catalog.Commands.CreateInventoryItem;

public class CreateInventoryItemCommandValidator : AbstractValidator<CreateInventoryItemCommand>
{
    public CreateInventoryItemCommandValidator()
    {
        RuleFor(x => x.ItemCategoryId)
            .GreaterThan(0).WithMessage("Please select an item category.");

        RuleFor(x => x.ItemName)
            .NotEmpty().WithMessage("Item name is required.")
            .MaximumLength(50).WithMessage("Item name must not exceed 50 characters.");

        RuleFor(x => x.InitialStock)
            .GreaterThanOrEqualTo(0).WithMessage("Initial stock cannot be negative.")
            .LessThanOrEqualTo(1000).WithMessage("Initial stock cannot exceed 1,000 units at once.");
    }
}