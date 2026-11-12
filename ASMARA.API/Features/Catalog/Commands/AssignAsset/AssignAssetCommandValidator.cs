using FluentValidation;

namespace ASMARA.API.Features.Catalog.Commands.AssignAsset;

public class AssignAssetCommandValidator : AbstractValidator<AssignAssetCommand>
{
    public AssignAssetCommandValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0);

        RuleFor(x => x.Request.EmployeeId)
            .NotEqual(Guid.Empty)
            .When(x => x.Request.EmployeeId.HasValue);
    }
}