using FluentValidation;

namespace ASMARA.API.Features.Tickets.Commands.ExecuteTicketAction;

public class ExecuteTicketActionCommandValidator : AbstractValidator<ExecuteTicketActionCommand>
{
    public ExecuteTicketActionCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(200);
    }
}