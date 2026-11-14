using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.AssignHandlerMaintenanceTicket;

public class AssignHandlerCommandValidator : AbstractValidator<AssignHandlerCommand>
{
    public AssignHandlerCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("Ticket ID is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        RuleFor(x => x.Request.TechnicianId)
            .NotEmpty().WithMessage("Please select a handler.");

        RuleFor(x => x.Request.InstructionNote)
            .MaximumLength(200).WithMessage("Instruction note must not exceed 200 characters.");
    }
}