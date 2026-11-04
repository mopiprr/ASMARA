using FluentValidation;

namespace ASMARA.API.Features.Procurement.Commands.CreateProcurementTicket;

public class CreateProcurementTicketValidator : AbstractValidator<CreateProcurementTicketCommand>
{
    public CreateProcurementTicketValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty();

        RuleFor(x => x.Request.Subject)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Request.Items)
            .NotEmpty()
            .Must(items => items != null && items.Count > 0);

        RuleForEach(x => x.Request.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0);
                
                item.RuleFor(i => i.Price)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Harga estimasi barang tidak boleh negatif.");

                // Validasi khusus untuk pengajuan barang manual (ItemId null)
                item.When(i => !i.ItemId.HasValue, () =>
                {
                    item.RuleFor(i => i.ItemName)
                        .NotEmpty();

                    item.RuleFor(i => i.ItemCategoryId)
                        .NotNull()
                        .GreaterThan(0);
                });
            });
    }
}
