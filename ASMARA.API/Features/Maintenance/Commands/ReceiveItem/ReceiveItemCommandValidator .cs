using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.ReceiveItem;

public class ReceiveItemCommandValidator : AbstractValidator<ReceiveItemCommand>
{
    public ReceiveItemCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(200);
    }
}
