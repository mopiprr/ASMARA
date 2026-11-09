using FluentValidation;

namespace ASMARA.API.Features.Employees.Commands.CreateEmployee;

public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();

        RuleFor(x => x.Request.DepartmentId).NotEmpty();

        RuleFor(x => x.Request.EmployeeNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.FullName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress().MaximumLength(50);
        RuleFor(x => x.Request.HireDate).NotEmpty();

        RuleFor(x => x.Request.UserName)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(@"^[a-zA-Z0-9._-]+$")
            .WithMessage("Username may only contain letters, numbers, dot, underscore and dash.");

        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
    }
}