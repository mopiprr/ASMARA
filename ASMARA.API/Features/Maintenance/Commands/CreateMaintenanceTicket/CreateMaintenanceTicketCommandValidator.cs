using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.CreateMaintenanceTicket;

public class CreateMaintenanceTicketCommandValidator : AbstractValidator<CreateMaintenanceTicketCommand>
{
    public CreateMaintenanceTicketCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        RuleFor(x => x.Request.AssetId)
            .GreaterThan(0).WithMessage("Please select the asset that needs maintenance.");

        RuleFor(x => x.Request.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(50).WithMessage("Subject must not exceed 50 characters.");

        RuleFor(x => x.Request.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.Request.ProblemDescription)
            .NotEmpty().WithMessage("Please describe the problem.")
            .MaximumLength(500).WithMessage("Problem description must not exceed 500 characters.");
    }
}