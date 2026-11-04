using ASMARA.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data.Repositories;

public class TicketRepository(AsmaraDbContext Context) : Repository<Ticket>(Context), ITicketRepository
{
    public async Task<string?> GetCurrentStepAsync(Guid ticketId, CancellationToken ct = default)
        => await Context.TicketWorkflowLogs
            .Where(x => x.TicketId == ticketId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => x.ToStep)
            .FirstOrDefaultAsync(ct);

    public async Task<string?> GetStepBeforeAsync(Guid ticketId, string step, CancellationToken ct = default)
        => await Context.TicketWorkflowLogs
            .Where(x => x.TicketId == ticketId && x.ToStep == step)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => x.FromStep)
            .FirstOrDefaultAsync(ct);
}