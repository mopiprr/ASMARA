using FluentValidation;

namespace ASMARA.API.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        RuleFor(x => x.Request.DepartmentId)
            .NotEmpty().WithMessage("Please select a department.");

        RuleFor(x => x.Request.EmployeeNumber)
            .NotEmpty().WithMessage("Employee number is required.")
            .MaximumLength(20).WithMessage("Employee number must not exceed 20 characters.");

        RuleFor(x => x.Request.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(50).WithMessage("Full name must not exceed 50 characters.");

        RuleFor(x => x.Request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Please enter a valid email address.")
            .MaximumLength(50).WithMessage("Email must not exceed 50 characters.");

        RuleFor(x => x.Request.HireDate)
            .NotEmpty().WithMessage("Hire date is required.");

        RuleFor(x => x.Request.UserName)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(50).WithMessage("Username must not exceed 50 characters.")
            .Matches(@"^[a-zA-Z0-9._-]+$")
            .WithMessage("Username may only contain letters, numbers, dot, underscore and dash.");

        RuleFor(x => x.Request.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters.");
    }
}