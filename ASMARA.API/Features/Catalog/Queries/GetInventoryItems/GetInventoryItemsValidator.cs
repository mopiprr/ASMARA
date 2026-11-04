using FluentValidation;

namespace ASMARA.API.Features.Catalog.Queries.GetInventoryItems;

public class GetInventoryItemsValidator : AbstractValidator<GetInventoryItemsQuery>
{
    public GetInventoryItemsValidator()
    {
        RuleFor(x => x.OnlyActive)
            .Equal(true);
        When(x => x.CategoryId.HasValue, () =>
        {
            RuleFor(x => x.CategoryId!.Value)
                .GreaterThan(0);
        });

        When(x => !string.IsNullOrWhiteSpace(x.Search), () =>
        {
            RuleFor(x => x.Search)
                .MaximumLength(100);
        });
    }
}
