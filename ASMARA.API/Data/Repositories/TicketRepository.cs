using ASMARA.API.Common.Exceptions;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
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

    public async Task<string> GenerateTicketNumberAsync(TicketTypeEnum type, CancellationToken ct = default)
    {
        var code = type == TicketTypeEnum.Maintenance ? "MNT" : "PRC";
        var prefix = $"{code}-{DateTime.Now:ddMMyy}-"; 
        var pattern = prefix + "%";

        var last = await Set
            .FromSqlInterpolated(
                $"SELECT * FROM dbo.Ticket WITH (UPDLOCK, HOLDLOCK) WHERE TicketNumber LIKE {pattern}")
            .Select(t => t.TicketNumber)
            .OrderByDescending(n => n)
            .FirstOrDefaultAsync(ct);

        var next = last is null ? 1 : int.Parse(last[^3..]) + 1;

        if (next > 999)
            throw new BusinessRuleException($"Daily ticket number limit reached for {code}.");

        return $"{prefix}{next:D3}"; 
    }
}