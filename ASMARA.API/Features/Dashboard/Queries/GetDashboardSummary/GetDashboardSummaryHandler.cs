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
            RecentAssets = recentAssets
        };
    }
}