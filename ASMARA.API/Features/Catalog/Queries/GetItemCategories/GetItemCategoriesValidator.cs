using FluentValidation;

namespace ASMARA.API.Features.Catalog.Queries.GetItemCategories;

public class GetItemCategoriesValidator : AbstractValidator<GetItemCategoriesQuery>
{
    public GetItemCategoriesValidator()
    {
        When(x => x.TrackingType.HasValue, () =>
        {
            RuleFor(x => x.TrackingType!.Value)
                .Must(v => v == 1 || v == 2)
                .WithMessage("TrackingType must be 1 (Consumable) or 2 (NonConsumable).");
        });
    }
}