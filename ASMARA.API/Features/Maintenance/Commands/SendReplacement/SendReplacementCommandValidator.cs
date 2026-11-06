using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.SendReplacement;

public class SendReplacementCommandValidator : AbstractValidator<SendReplacementCommand>
{
    public SendReplacementCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.ReplacementAssetId).GreaterThan(0);
        RuleFor(x => x.Request.Note).MaximumLength(200);
    }
}