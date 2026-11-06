using FluentValidation;

namespace ASMARA.API.Features.Procurement.Commands.CreateProcurementTicket;

public class CreateProcurementTicketValidator : AbstractValidator<CreateProcurementTicketCommand>
{
    public CreateProcurementTicketValidator()
    {
        RuleFor(x => x.Request)
            .NotNull();

        RuleFor(x => x.Request.Subject)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Request.Description)
            .MaximumLength(500);

        RuleFor(x => x.Request.Items)
            .NotEmpty()
            .Must(items => items != null && items.Count > 0);
        
        RuleForEach(x => x.Request.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.Quantity)
                    .GreaterThan(0);

                item.RuleFor(i => i.Price)
                    .GreaterThan(0);
                
                // Validasi khusus untuk pengajuan barang manual (ItemId null)
                item.When(i => !i.ItemId.HasValue, () =>
                {
                    item.RuleFor(i => i.ItemName)
                        .NotEmpty();

                    // item.RuleFor(i => i.ItemCategoryId)
                    //     .NotNull()
                    //     .GreaterThan(0);
                    item.RuleFor(i => i.ItemCategoryId)
                        .GreaterThan(0);
                });
            });
    }
}
