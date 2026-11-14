using FluentValidation;

namespace ASMARA.API.Features.Catalog.Queries.GetAssets;

public class GetAssetsQueryValidator : AbstractValidator<GetAssetsQuery>
{
    private static readonly string[] SortFields = ["assettag", "itemname", "category", "holder", "status"];

    public GetAssetsQueryValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid asset status filter.")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.Search)
            .MaximumLength(50).WithMessage("Search keyword must not exceed 50 characters.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.SortBy)
            .Must(s => SortFields.Contains(s!.ToLowerInvariant()))
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy))
            .WithMessage("SortBy must be one of: assetTag, itemName, category, holder, status.");
    }
}