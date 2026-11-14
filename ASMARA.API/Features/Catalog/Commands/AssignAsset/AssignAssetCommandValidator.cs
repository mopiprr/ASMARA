using FluentValidation;

namespace ASMARA.API.Features.Catalog.Commands.AssignAsset;

public class AssignAssetCommandValidator : AbstractValidator<AssignAssetCommand>
{
    public AssignAssetCommandValidator()
    {
        RuleFor(x => x.AssetId)
            .GreaterThan(0).WithMessage("Invalid asset ID.");

        // EmployeeId null = aset publik (dipakai bersama).
        RuleFor(x => x.Request.EmployeeId)
            .NotEqual(Guid.Empty).WithMessage("Please select a valid employee, or choose Public.")
            .When(x => x.Request.EmployeeId.HasValue);
    }
}