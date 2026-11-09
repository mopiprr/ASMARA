using FluentValidation;

namespace ASMARA.API.Features.Catalog.Queries.GetAssets;

public class GetAssetsQueryValidator : AbstractValidator<GetAssetsQuery>
{
    private static readonly string[] SortFields = ["assettag", "itemname", "category", "holder", "status"];

    public GetAssetsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Search).MaximumLength(50);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.SortBy)
            .Must(s => SortFields.Contains(s!.ToLowerInvariant()))
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy))
            .WithMessage("SortBy must be one of: assetTag, itemName, category, holder, status.");
    }
}