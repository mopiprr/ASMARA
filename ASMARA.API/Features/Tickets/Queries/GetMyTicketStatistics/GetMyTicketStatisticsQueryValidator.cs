using FluentValidation;

namespace ASMARA.API.Features.Tickets.Queries.GetMyTicketStatistics;

public class GetMyTicketStatisticsQueryValidator : AbstractValidator<GetMyTicketStatisticsQuery>
{
    public GetMyTicketStatisticsQueryValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid ticket type. Use 1 (Maintenance) or 2 (Procurement).")
            .When(x => x.Type.HasValue);
    }
}