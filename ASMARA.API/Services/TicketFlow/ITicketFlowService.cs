using ASMARA.API.Enums;

namespace ASMARA.API.Services.TicketFlow;

public record TransitionResult(
    Guid TicketId,
    string FromStep,
    string ToStep,
    bool IsTerminal,
    Guid? NextTaskEmployeeId, 
    int? NextLevel);

public interface ITicketFlowService
{
    Task<TransitionResult> ExecuteAsync(Guid ticketId, TicketActionEnum action, string? note = null, CancellationToken ct = default);
}
