using FluentValidation;

namespace ASMARA.API.Features.Dashboard.Queries.GetMyActivityTickets;

public class GetMyActivityTicketsQueryValidator : AbstractValidator<GetMyActivityTicketsQuery>
{
    public GetMyActivityTicketsQueryValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid ticket type. Use 1 (Maintenance) or 2 (Procurement).")
            .When(x => x.Type.HasValue);

        RuleFor(x => x.Search)
            .MaximumLength(50).WithMessage("Search keyword must not exceed 50 characters.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("End date must be on or after the start date.");
    }
}