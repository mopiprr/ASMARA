using FluentValidation;

namespace ASMARA.API.Features.Catalog.Commands.DeleteInventoryUnits;

public class DeleteInventoryUnitsCommandValidator : AbstractValidator<DeleteInventoryUnitsCommand>
{
    public DeleteInventoryUnitsCommandValidator()
    {
        RuleFor(x => x.ItemId)
            .GreaterThan(0).WithMessage("Invalid item ID.");

        RuleFor(x => x.Dto.Reason)
            .NotEmpty().WithMessage("Please enter the reason for removing these units.")
            .MaximumLength(150).WithMessage("Reason must not exceed 150 characters.");

        RuleFor(x => x.Dto.Notes)
            .MaximumLength(500).WithMessage("Notes must not exceed 500 characters.");
    }
}