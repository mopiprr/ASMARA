using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Ticket;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Services.TicketFlow;

public class TicketHistoryService(IUnitOfWork uow) : ITicketHistoryService
{
    public async Task<List<TicketWorkflowHistoryDto>> GetHistoryAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticketType = await uow.Tickets.QueryNoTracking()
            .Where(t => t.Id == ticketId)
            .Select(t => (byte?)t.TicketType)
            .FirstOrDefaultAsync(ct);

        if (ticketType is null)
            return [];

        var type = (TicketTypeEnum)ticketType.Value;

        var rawLogs = await uow.TicketWorkflowLogs.QueryNoTracking()
            .Where(l => l.TicketId == ticketId)
            .OrderBy(l => l.CreatedAt).ThenBy(l => l.Id)
            .Select(l => new { l.FromStep, l.ToStep, l.EmployeeId, l.EmployeeName, l.Note, l.CreatedAt })
            .ToListAsync(ct);

        return rawLogs.Select(l =>
        {
            var action = TicketHistoryMapper.ResolveAction(type, l.FromStep, l.ToStep);
            return new TicketWorkflowHistoryDto(
                action?.ToString(),
                TicketHistoryMapper.LabelOf(action, l.FromStep, l.ToStep),
                l.FromStep,
                l.ToStep,
                l.EmployeeId,
                l.EmployeeName,
                l.Note,
                l.CreatedAt);
        }).ToList();
    }
}
