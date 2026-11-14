using FluentValidation;

namespace ASMARA.API.Features.Catalog.Queries.GetInventoryItems;

public class GetInventoryItemsValidator : AbstractValidator<GetInventoryItemsQuery>
{
    public GetInventoryItemsValidator()
    {
        When(x => x.CategoryId.HasValue, () =>
        {
            RuleFor(x => x.CategoryId!.Value)
                .GreaterThan(0).WithMessage("Invalid category filter.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Search), () =>
        {
            RuleFor(x => x.Search)
                .MaximumLength(100).WithMessage("Search keyword must not exceed 100 characters.");
        });

        When(x => x.TrackingType.HasValue, () =>
        {
            RuleFor(x => x.TrackingType!.Value)
                .Must(v => v == 1 || v == 2)
                .WithMessage("TrackingType must be 1 (Consumable) or 2 (NonConsumable).");
        });
    }
}