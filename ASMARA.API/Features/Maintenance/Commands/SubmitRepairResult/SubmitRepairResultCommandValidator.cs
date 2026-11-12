
using ASMARA.API.Enums;
using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.SubmitRepairResult;

public class SubmitRepairResultCommandValidator
    : AbstractValidator<SubmitRepairResultCommand>
{
    public SubmitRepairResultCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty();

        RuleFor(x => x.Request)
            .NotNull();

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.Result)
                .IsInEnum();

            RuleFor(x => x.Request.HandlerNote)
                .MaximumLength(500);

            RuleFor(x => x.Request.PartRequirement)
                .MaximumLength(500);

            When(
                x => x.Request.Result == RepairResultEnum.NeedPart,
                () =>
                {
                    RuleFor(x => x.Request)
                        .Must(request =>
                            request.Parts is { Count: > 0 } ||
                            !string.IsNullOrWhiteSpace(request.PartRequirement))
                        .WithMessage(
                            "At least one spare part is required when repair result is NeedPart.");

                    RuleForEach(x => x.Request.Parts)
                        .ChildRules(part =>
                        {
                            part.RuleFor(p => p.ItemName)
                                .NotEmpty()
                                .MaximumLength(200);

                            part.RuleFor(p => p.Quantity)
                                .GreaterThan(0);

                            part.RuleFor(p => p.ItemCategoryId)
                                .GreaterThan(0);
                        })
                        .When(x => x.Request.Parts is { Count: > 0 });
                });
        });
    }
}
