using FluentValidation;

namespace ASMARA.API.Features.Catalog.Queries.GetItemCategories;

public class GetItemCategoriesValidator : AbstractValidator<GetItemCategoriesQuery>
{
    public GetItemCategoriesValidator()
    {
        RuleFor(x => x.OnlyActive)
            .Equal(true);
    }
}
