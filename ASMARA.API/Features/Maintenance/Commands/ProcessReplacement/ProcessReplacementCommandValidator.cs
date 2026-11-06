using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.ProcessReplacement;

public class ProcessReplacementCommandValidator : AbstractValidator<ProcessReplacementCommand>
{
    public ProcessReplacementCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Note).MaximumLength(200);

        RuleFor(x => x.Request.ReplacementAssetId)
            .NotNull().GreaterThan(0)
            .When(x => x.Request.Available)
            .WithMessage("ReplacementAssetId is required when a replacement is available.");
    }
}