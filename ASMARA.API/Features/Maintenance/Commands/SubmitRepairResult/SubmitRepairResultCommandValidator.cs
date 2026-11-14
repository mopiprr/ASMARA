using ASMARA.API.Enums;
using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.SubmitRepairResult;

public class SubmitRepairResultCommandValidator : AbstractValidator<SubmitRepairResultCommand>
{
    public SubmitRepairResultCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("Ticket ID is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.Result)
                .IsInEnum().WithMessage("Invalid repair result. Choose Repaired, Need Part, or Unrepairable.");

            RuleFor(x => x.Request.HandlerNote)
                .MaximumLength(500).WithMessage("Handler note must not exceed 500 characters.");

            RuleFor(x => x.Request.PartRequirement)
                .MaximumLength(500).WithMessage("Part requirement must not exceed 500 characters.");

            When(x => x.Request.Result == RepairResultEnum.NeedPart, () =>
            {
                RuleFor(x => x.Request)
                    .Must(request =>
                        request.Parts is { Count: > 0 } ||
                        !string.IsNullOrWhiteSpace(request.PartRequirement))
                    .WithMessage("Please add at least one spare part when the result is Need Part.");

                RuleForEach(x => x.Request.Parts)
                    .ChildRules(part =>
                    {
                        part.RuleFor(p => p.ItemName)
                            .NotEmpty().WithMessage("Spare part name is required.")
                            .MaximumLength(50).WithMessage("Spare part name must not exceed 50 characters.");

                        part.RuleFor(p => p.Quantity)
                            .GreaterThan(0).WithMessage("Spare part quantity must be greater than 0.");

                        part.RuleFor(p => p.ItemCategoryId)
                            .GreaterThan(0).WithMessage("Please select a category for the spare part.");
                    })
                    .When(x => x.Request.Parts is { Count: > 0 });
            });
        });
    }
}