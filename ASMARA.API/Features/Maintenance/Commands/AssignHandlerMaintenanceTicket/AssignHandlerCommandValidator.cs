using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.AssignHandlerMaintenanceTicket;

public class AssignHandlerCommandValidator : AbstractValidator<AssignHandlerCommand>
{
    public AssignHandlerCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.TechnicianId).NotEmpty();
        RuleFor(x => x.Request.InstructionNote).MaximumLength(200);
    }
}