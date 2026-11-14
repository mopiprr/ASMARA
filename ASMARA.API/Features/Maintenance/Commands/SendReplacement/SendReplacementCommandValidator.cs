using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.SendReplacement;

public class SendReplacementCommandValidator : AbstractValidator<SendReplacementCommand>
{
    public SendReplacementCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("Ticket ID is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        RuleFor(x => x.Request.ReplacementAssetId)
            .GreaterThan(0).WithMessage("Please select a replacement asset.");

        RuleFor(x => x.Request.Note)
            .MaximumLength(200).WithMessage("Note must not exceed 200 characters.");
    }
}