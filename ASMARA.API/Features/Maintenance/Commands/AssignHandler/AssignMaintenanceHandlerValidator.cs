using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.AssignHandler;

public class AssignMaintenanceHandlerValidator : AbstractValidator<AssignMaintenanceHandlerCommand>
{
    public AssignMaintenanceHandlerValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Request.TechnicianId).NotEmpty();
        RuleFor(x => x.Request.InstructionNote).MaximumLength(200);
    }
}
