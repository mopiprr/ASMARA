
using ASMARA.API.Data;
using ASMARA.API.Enums;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace ASMARA.API.Features.Dashboard.Queries.GetDashboardSummary;

public class GetDashboardSummaryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryResponse>
{
    private readonly AsmaraDbContext _context;

    public GetDashboardSummaryHandler(AsmaraDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryResponse> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        // Mengambil statistik aset.
        var statistics = new AssetStatisticsResponse
        {
            TotalAssets = await _context.Assets.CountAsync(cancellationToken),

            Available = await _context.Assets.CountAsync(
                x => x.Status == (byte)AssetStatusEnum.Available,
                cancellationToken),

            InUse = await _context.Assets.CountAsync(
                x => x.Status == (byte)AssetStatusEnum.InUse,
                cancellationToken),

            UnderMaintenance = await _context.Assets.CountAsync(
                x => x.Status == (byte)AssetStatusEnum.UnderMaintenance,
                cancellationToken),

            Disposed = await _context.Assets.CountAsync(
                x => x.Status == (byte)AssetStatusEnum.Disposed,
                cancellationToken),

            Archived = await _context.Assets.CountAsync(
                x => x.Status == (byte)AssetStatusEnum.Archived,
                cancellationToken)
        };

        // Mengambil lima aset terbaru.
        var recentAssets = await _context.Assets
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(5)
            .Select(x => new RecentAssetResponse
            {
                Id = x.Id,
                AssetTag = x.AssetTag,
                ItemName = x.Item.ItemName,
                EmployeeName = x.Employee != null
                    ? x.Employee.FullName
                    : null,
                Status = x.Status == (byte)AssetStatusEnum.Available
                    ? "Available"
                    : x.Status == (byte)AssetStatusEnum.InUse
                        ? "In Use"
                        : x.Status == (byte)AssetStatusEnum.UnderMaintenance
                            ? "Under Maintenance"
                            : x.Status == (byte)AssetStatusEnum.Disposed
                                ? "Disposed"
                                : x.Status == (byte)AssetStatusEnum.Archived
                                    ? "Archived"
                                    : "Unknown"
            })
            .ToListAsync(cancellationToken);

        // Mengambil lima tiket terbaru milik employee yang sedang login.
        var ticketData = await _context.Tickets
            .AsNoTracking()
            .Where(x => x.EmployeeId == request.EmployeeId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                x.TicketNumber,
                x.Subject,
                x.TicketType,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var recentTickets = ticketData
            .Select(x => new RecentTicketResponse
            {
                Id = x.Id,
                TicketNumber = x.TicketNumber,
                Subject = x.Subject,
                TicketType = Enum.GetName(
                    typeof(TicketTypeEnum),
                    Enum.ToObject(typeof(TicketTypeEnum), x.TicketType)
                ) ?? "Unknown",
                CreatedAt = x.CreatedAt
            })
            .ToList();

        return new DashboardSummaryResponse
        {
            Profile = new UserProfileResponse
            {
                EmployeeId = request.EmployeeId,
                EmployeeNumber = request.EmployeeNumber,
                FullName = request.FullName,
                Email = request.Email,
                Roles = request.Roles
            },

            Statistics = statistics,
            RecentAssets = recentAssets,
            RecentTickets = recentTickets
        };
    }
}
