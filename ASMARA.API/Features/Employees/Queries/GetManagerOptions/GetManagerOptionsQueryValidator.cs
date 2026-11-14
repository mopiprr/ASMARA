using FluentValidation;

namespace ASMARA.API.Features.Employees.Queries.GetManagerOptions;

public class GetManagerOptionsQueryValidator : AbstractValidator<GetManagerOptionsQuery>
{
    public GetManagerOptionsQueryValidator()
    {
        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("Please select a department first.");
    }
}