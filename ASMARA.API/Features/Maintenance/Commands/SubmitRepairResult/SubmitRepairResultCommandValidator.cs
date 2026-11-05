using ASMARA.API.Enums;
using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.SubmitRepairResult;

public class SubmitRepairResultCommandValidator : AbstractValidator<SubmitRepairResultCommand>
{
    public SubmitRepairResultCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Result).IsInEnum();
        RuleFor(x => x.Request.HandlerNote).MaximumLength(500); 

        RuleFor(x => x.Request.PartRequirement)
            .NotEmpty().WithMessage("PartRequirement is required when the result is NeedPart.")
            .When(x => x.Request.Result == RepairResultEnum.NeedPart);
        RuleFor(x => x.Request.PartRequirement).MaximumLength(500);
    }
}