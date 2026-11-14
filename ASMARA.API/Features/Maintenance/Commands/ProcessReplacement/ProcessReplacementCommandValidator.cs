using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.ProcessReplacement;

public class ProcessReplacementCommandValidator : AbstractValidator<ProcessReplacementCommand>
{
    public ProcessReplacementCommandValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("Ticket ID is required.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        RuleFor(x => x.Request.Note)
            .MaximumLength(200).WithMessage("Note must not exceed 200 characters.");

        // Pengganti tersedia di gudang -> wajib pilih asset.
        When(x => x.Request.Available, () =>
        {
            RuleFor(x => x.Request.ReplacementAssetId)
                .NotNull().WithMessage("Please select a replacement asset.")
                .GreaterThan(0).WithMessage("Please select a replacement asset.");
        });

        // Pengganti tidak ada -> data pembelian (Procurement child) wajib diisi.
        When(x => !x.Request.Available, () =>
        {
            RuleFor(x => x.Request.Subject)
                .NotEmpty().WithMessage("Purchase request subject is required.")
                .MaximumLength(50).WithMessage("Purchase request subject must not exceed 50 characters.");

            RuleFor(x => x.Request.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

            RuleFor(x => x.Request.Items)
                .NotEmpty().WithMessage("Please add at least one item to purchase.");

            RuleForEach(x => x.Request.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0).WithMessage("Quantity must be greater than 0.");

                item.RuleFor(i => i.Price)
                    .GreaterThan(0).WithMessage("Estimated price must be greater than 0.");

                item.When(i => i.ItemId is null, () =>
                {
                    item.RuleFor(i => i.ItemName)
                        .NotEmpty().WithMessage("Item name is required for a manual item.")
                        .MaximumLength(50).WithMessage("Item name must not exceed 50 characters.");

                    item.RuleFor(i => i.ItemCategoryId)
                        .GreaterThan(0).WithMessage("Please select an item category for a manual item.");
                });
            });
        });
    }
}