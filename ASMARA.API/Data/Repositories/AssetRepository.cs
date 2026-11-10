using ASMARA.API.Data;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data.Repositories;

public class AssetRepository(AsmaraDbContext context) : Repository<Asset>(context), IAssetRepository
{
    public IQueryable<Asset> GetAllAvailableAssetForMaintenance(Guid employeeId)
        => Set.Where(a => a.Status == (byte)AssetStatusEnum.InUse && (a.EmployeeId == employeeId || a.EmployeeId == null));

    public async Task<bool> TryClaimForMaintenanceAsync(int assetId, Guid employeeId, CancellationToken ct = default)
    {
        var affected = await GetAllAvailableAssetForMaintenance(employeeId)
            .Where(a => a.Id == assetId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, (byte)AssetStatusEnum.UnderMaintenance), ct);

        return affected == 1;
    }

    public async Task<bool> ReleaseFromMaintenanceAsync(int assetId, CancellationToken ct = default)
    {
        var affected = await Set
            .Where(a => a.Id == assetId && a.Status == (byte)AssetStatusEnum.UnderMaintenance)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, (byte)AssetStatusEnum.InUse), ct);

        return affected == 1;
    }

    public async Task<bool> RetireAsync(int assetId, AssetStatusEnum newStatus, CancellationToken ct = default)
    {
        var affected = await Set
            .Where(a => a.Id == assetId && a.Status == (byte)AssetStatusEnum.UnderMaintenance)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, (byte)newStatus), ct);

        return affected == 1;
    }

    public async Task<bool> TryAssignReplacementAsync(int assetId, Guid? ownerId, CancellationToken ct = default)
    {
        var affected = await Set
            .Where(a => a.Id == assetId && a.Status == (byte)AssetStatusEnum.Available)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, (byte)AssetStatusEnum.InUse)
                .SetProperty(a => a.EmployeeId, ownerId), ct);

        return affected == 1;
    }

    public async Task<bool> TryAssignToEmployeeAsync(int assetId, Guid? employeeId, CancellationToken ct = default)
    {
        var affected = await Set
            .Where(a => a.Id == assetId && (a.Status == (byte)AssetStatusEnum.Available || a.Status == (byte)AssetStatusEnum.InUse))
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.EmployeeId, employeeId)
                .SetProperty(a => a.Status, (byte)AssetStatusEnum.InUse), ct);

        return affected == 1;
    }
}