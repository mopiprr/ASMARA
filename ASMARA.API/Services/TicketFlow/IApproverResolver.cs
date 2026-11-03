using ASMARA.API.Entities;

namespace ASMARA.API.Services.TicketFlow;

public interface IApproverResolver
{
    Task<Guid?> ResolveAsync(Ticket ticket, int? level, CancellationToken ct = default);
}
