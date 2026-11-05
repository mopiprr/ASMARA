using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.RejectMaintenanceTicket;

public class RejectMaintenanceTicketCommandValidator : AbstractValidator<RejectMaintenanceTicketCommand>
{
    public RejectMaintenanceTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(200);
    }
}