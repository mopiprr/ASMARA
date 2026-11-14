using FluentValidation;

namespace ASMARA.API.Features.Employees.Queries.GetEmployees;

public class GetEmployeesQueryValidator : AbstractValidator<GetEmployeesQuery>
{
    private static readonly string[] SortFields = ["employeenumber", "fullname", "department", "status"];

    public GetEmployeesQueryValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid employee status filter.")
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
            .WithMessage("SortBy must be one of: employeeNumber, fullName, department, status.");
    }
}