using ASMARA.API.Entities;
using ASMARA.API.Enums;

namespace ASMARA.API.Services.TicketFlow;

public record TransitionResult(
    Guid TicketId,
    string FromStep,
    string ToStep,
    bool IsTerminal,
    Guid? NextTaskEmployeeId, 
    int? NextLevel);

public record ChildTicketResult(TransitionResult Parent, TransitionResult Child);

public interface ITicketFlowService
{
    Task<TransitionResult> ExecuteAsync(Guid ticketId, TicketActionEnum action, string? note = null, CancellationToken ct = default);
    Task<ChildTicketResult> CreateChildAsync(Guid parentTicketId, TicketActionEnum parentAction, Ticket child, string? note = null, CancellationToken ct = default);
}
