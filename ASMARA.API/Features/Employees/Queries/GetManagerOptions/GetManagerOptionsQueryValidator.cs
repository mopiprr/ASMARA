using FluentValidation;

namespace ASMARA.API.Features.Employees.Queries.GetManagerOptions;

public class GetManagerOptionsQueryValidator : AbstractValidator<GetManagerOptionsQuery>
{
    public GetManagerOptionsQueryValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty();
    }
}