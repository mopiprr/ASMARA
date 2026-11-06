namespace ASMARA.API.Models.Dashboard;

public class DashboardDto
{
}
public record ActivityTicketListItemDto(
    Guid Id,
    string TicketNumber,
    byte TicketType,
    string Subject,
    string RequesterName,
    string CurrentStep,
    string CurrentStepName,
    string MyLastAction,    
    DateTime MyLastActionAt);

public record TicketStatisticsDto(
    int TotalTickets,
    int PendingTickets,
    int InProgressTickets,
    int CompletedTickets
);