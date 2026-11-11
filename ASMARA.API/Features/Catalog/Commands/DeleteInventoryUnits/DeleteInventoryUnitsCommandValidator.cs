using FluentValidation;

namespace ASMARA.API.Features.Catalog.Commands.DeleteInventoryUnits;

public class DeleteInventoryUnitsCommandValidator : AbstractValidator<DeleteInventoryUnitsCommand>
{
    public DeleteInventoryUnitsCommandValidator()
    {
        RuleFor(x => x.ItemId)
            .GreaterThan(0)
            .WithMessage("Item ID must be greater than 0.");

        RuleFor(x => x.Dto.Reason)
            .NotEmpty()
            .WithMessage("Reason for deletion/reduction is required.")
            .MaximumLength(150)
            .WithMessage("Reason cannot exceed 150 characters.");

        RuleFor(x => x.Dto.Notes)
            .MaximumLength(500)
            .WithMessage("Notes cannot exceed 500 characters.");
    }
}
