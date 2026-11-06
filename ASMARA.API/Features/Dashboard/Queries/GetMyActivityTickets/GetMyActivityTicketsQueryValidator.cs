using FluentValidation;

namespace ASMARA.API.Features.Dashboard.Queries.GetMyActivityTickets;

public class GetMyActivityTicketsQueryValidator : AbstractValidator<GetMyActivityTicketsQuery>
{
    public GetMyActivityTicketsQueryValidator()
    {
        RuleFor(x => x.Type).IsInEnum().When(x => x.Type.HasValue);
        RuleFor(x => x.Search).MaximumLength(50);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("ToDate must be on or after FromDate.");
    }
}