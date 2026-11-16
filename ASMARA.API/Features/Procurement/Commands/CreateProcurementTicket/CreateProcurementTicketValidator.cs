using FluentValidation;

namespace ASMARA.API.Features.Procurement.Commands.CreateProcurementTicket;

public class CreateProcurementTicketValidator : AbstractValidator<CreateProcurementTicketCommand>
{
    public CreateProcurementTicketValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request body is required.");

        RuleFor(x => x.Request.Subject)
            .NotEmpty().WithMessage("Request Subject is required.")
            .MaximumLength(50).WithMessage("Request Subject must not exceed 50 characters.");

        RuleFor(x => x.Request.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.Request.Items)
            .NotEmpty().WithMessage("Please add at least one procurement item.");

        RuleForEach(x => x.Request.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0).WithMessage("Quantity must be greater than 0.");

                item.RuleFor(i => i.Price)
                    .GreaterThan(0).WithMessage("Estimated price must be greater than 0.");

                // Validasi khusus untuk pengajuan barang manual (ItemId null)
                item.When(i => !i.ItemId.HasValue, () =>
                {
                    item.RuleFor(i => i.ItemName)
                        .NotEmpty().WithMessage("Item name is required for a manual item.")
                        .MaximumLength(50).WithMessage("Item name must not exceed 50 characters.");

                    item.RuleFor(i => i.ItemCategoryId)
                        .GreaterThan(0).WithMessage("Please select an item category for a manual item.");
                });
            });
    }
}