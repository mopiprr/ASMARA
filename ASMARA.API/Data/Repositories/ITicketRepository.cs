using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public interface ITicketRepository : IRepository<Ticket>
{
    Task<string?> GetCurrentStepAsync(Guid ticketId, CancellationToken ct = default);
    Task<string?> GetStepBeforeAsync(Guid ticketId, string step, CancellationToken ct = default);
}
