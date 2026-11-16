using FluentValidation;

namespace ASMARA.API.Features.Procurement.Commands.ReceiveProcurementGoods;

public class ReceiveProcurementGoodsValidator : AbstractValidator<ReceiveProcurementGoodsCommand>
{
    private const long MaxProofSize = 5 * 1024 * 1024;

    public ReceiveProcurementGoodsValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("Ticket ID is required.");

        RuleFor(x => x.Note)
            .MaximumLength(200).WithMessage("Note must not exceed 200 characters.");

        RuleFor(x => x.ProofFile)
            .NotNull().WithMessage("Purchase proof is required.");

        RuleFor(x => x.ProofFile.Length)
            .GreaterThan(0).WithMessage("Purchase proof file is empty.")
            .LessThanOrEqualTo(MaxProofSize).WithMessage("Purchase proof must not exceed 5 MB.")
            .When(x => x.ProofFile is not null);

        RuleFor(x => x.ProofFile.FileName)
            .MaximumLength(255).WithMessage("Purchase proof file name must not exceed 255 characters.")
            .When(x => x.ProofFile is not null);
    }
}