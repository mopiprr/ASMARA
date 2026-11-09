using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using ASMARA.API.Services.TicketFlow;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Tickets.Queries.GetMyTicketStatistics;

public record GetMyTicketStatisticsQuery(TicketTypeEnum? Type = null)
    : IRequest<MyTicketStatisticsDto>;

public class GetMyTicketStatisticsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    : IRequestHandler<GetMyTicketStatisticsQuery, MyTicketStatisticsDto>
{
    public async Task<MyTicketStatisticsDto> Handle(GetMyTicketStatisticsQuery request, CancellationToken ct)
    {
        var employeeId = currentUser.EmployeeId;

        var query = uow.Tickets.QueryNoTracking()
            .Where(t => t.EmployeeId == employeeId);

        if (request.Type.HasValue)
        {
            var type = (byte)request.Type.Value;
            query = query.Where(t => t.TicketType == type);
        }

        var total = await query.CountAsync(ct);

        var active = await query.CountAsync(t => t.ClosedAt == null, ct);

        var rejected = await query.CountAsync(t =>
            t.ClosedAt != null &&
            t.TicketWorkflowLogs.Any(l => l.ToStep == StepCodes.Rejected), ct);

        return new MyTicketStatisticsDto(
            total,
            active,
            total - active,
            rejected);
    }
}