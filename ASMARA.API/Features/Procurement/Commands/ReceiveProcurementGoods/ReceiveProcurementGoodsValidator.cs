using FluentValidation;

namespace ASMARA.API.Features.Procurement.Commands.ReceiveProcurementGoods;

public class ReceiveProcurementGoodsValidator : AbstractValidator<ReceiveProcurementGoodsCommand>
{
    public ReceiveProcurementGoodsValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(200);
    }
}
