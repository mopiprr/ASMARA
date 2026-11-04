namespace ASMARA.API.Features.Dashboard.Queries.GetDashboardSummary;

public class DashboardSummaryResponse
{
    public UserProfileResponse Profile { get; set; } = new();

    public AssetStatisticsResponse Statistics { get; set; } = new();

    public List<RecentAssetResponse> RecentAssets { get; set; } = new();
    public List<RecentTicketResponse> RecentTickets { get; set; } = [];
}

public class UserProfileResponse
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
}

public class AssetStatisticsResponse
{
    public int TotalAssets { get; set; }
    public int Available { get; set; }
    public int InUse { get; set; }
    public int UnderMaintenance { get; set; }
    public int Disposed { get; set; }
    public int Archived { get; set; }
}

public class RecentAssetResponse
{
    public int Id { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string Status { get; set; } = string.Empty;
}