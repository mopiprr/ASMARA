using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.PartCheck;

public class PartCheckCommandValidator : AbstractValidator<PartCheckCommand>
{
    public PartCheckCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("Ticket ID is required.");

        RuleFor(x => x.Note)
            .MaximumLength(200).WithMessage("Note must not exceed 200 characters.");
    }
}