namespace ASMARA.API.Features.Dashboard.Queries.GetDashboardSummary;

public class RecentTicketResponse
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TicketType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}