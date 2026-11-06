using FluentValidation;

namespace ASMARA.API.Features.Maintenance.Commands.OrderPart;

public class OrderPartCommandValidator : AbstractValidator<OrderPartCommand>
{
    public OrderPartCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Subject).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Request.Description).MaximumLength(500);
        RuleFor(x => x.Request.Note).MaximumLength(200);
        RuleFor(x => x.Request.Items).NotEmpty();

        RuleForEach(x => x.Request.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.Price).GreaterThan(0);
            item.When(i => i.ItemId is null, () =>
            {
                item.RuleFor(i => i.ItemName).NotEmpty().MaximumLength(50);
                item.RuleFor(i => i.ItemCategoryId).GreaterThan(0);
            });
        });
    }
}