using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.CreateMaintenanceTicket;

public class CreateMaintenanceTicketCommandValidator : AbstractValidator<CreateMaintenanceTicketCommand>
{
    public CreateMaintenanceTicketCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();

        RuleFor(x => x.Request.AssetId).GreaterThan(0);
        RuleFor(x => x.Request.Subject).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Request.Description).MaximumLength(500);
        RuleFor(x => x.Request.ProblemDescription).NotEmpty().MaximumLength(500);
    }
}