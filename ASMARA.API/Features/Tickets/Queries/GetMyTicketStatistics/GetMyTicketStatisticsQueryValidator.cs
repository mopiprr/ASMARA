using FluentValidation;

namespace ASMARA.API.Features.Tickets.Queries.GetMyTicketStatistics;

public class GetMyTicketStatisticsQueryValidator : AbstractValidator<GetMyTicketStatisticsQuery>
{
    public GetMyTicketStatisticsQueryValidator()
    {
        RuleFor(x => x.Type).IsInEnum().When(x => x.Type.HasValue);
    }
}