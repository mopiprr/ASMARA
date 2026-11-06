using ASMARA.API.Models.Ticket;

namespace ASMARA.API.Services.TicketFlow;

public interface ITicketHistoryService
{
    Task<List<TicketWorkflowHistoryDto>> GetHistoryAsync(Guid ticketId, CancellationToken ct = default);
}
