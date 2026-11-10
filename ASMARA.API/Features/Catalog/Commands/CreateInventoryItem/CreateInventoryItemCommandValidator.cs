using FluentValidation;

namespace ASMARA.API.Features.Catalog.Commands.CreateInventoryItem;

public class CreateInventoryItemCommandValidator : AbstractValidator<CreateInventoryItemCommand>
{
    public CreateInventoryItemCommandValidator()
    {
        RuleFor(x => x.ItemCategoryId)
            .GreaterThan(0)
            .WithMessage("Item category is required.");

        RuleFor(x => x.ItemName)
            .NotEmpty()
            .WithMessage("Item name is required.")
            .MaximumLength(100)
            .WithMessage("Item name cannot exceed 100 characters.");

        RuleFor(x => x.InitialStock)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Initial stock cannot be negative.")
            .LessThanOrEqualTo(1000)
            .WithMessage("Initial stock batch cannot exceed 1000 units at once.");
    }
}
