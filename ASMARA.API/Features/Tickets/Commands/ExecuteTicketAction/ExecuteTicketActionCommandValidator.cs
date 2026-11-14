using ASMARA.API.Enums;
using FluentValidation;

namespace ASMARA.API.Features.Tickets.Commands.ExecuteTicketAction;

public class ExecuteTicketActionCommandValidator : AbstractValidator<ExecuteTicketActionCommand>
{
    public ExecuteTicketActionCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("Ticket ID is required.");

        RuleFor(x => x.Note)
            .MaximumLength(200).WithMessage("Note must not exceed 200 characters.");

        // Procurement: Revise & Reject wajib ada alasan untuk requester.
        RuleFor(x => x.Note)
            .NotEmpty().WithMessage("Please write a note explaining the reason for this action.")
            .When(x => x.ExpectedType == TicketTypeEnum.Procurement
                    && x.Action is TicketActionEnum.Revise or TicketActionEnum.Reject);
    }
}