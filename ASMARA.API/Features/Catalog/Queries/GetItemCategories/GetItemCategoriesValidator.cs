using FluentValidation;

namespace ASMARA.API.Features.Catalog.Queries.GetItemCategories;

public class GetItemCategoriesValidator : AbstractValidator<GetItemCategoriesQuery>
{
    public GetItemCategoriesValidator()
    {
    }
}
