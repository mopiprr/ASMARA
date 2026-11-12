
using ASMARA.API.Data;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data.Repositories;

public class AssetRepository(AsmaraDbContext context)
    : Repository<Asset>(context), IAssetRepository
{
    // Asset milik karyawan: harus InUse.
    // Asset shared: harus Available.
    public IQueryable<Asset> GetAllAvailableAssetForMaintenance(Guid employeeId)
        => Set.Where(a =>
            (
                a.EmployeeId == employeeId &&
                a.Status == (byte)AssetStatusEnum.InUse
            )
            ||
            (
                a.EmployeeId == null &&
                a.Status == (byte)AssetStatusEnum.Available
            ));

    public async Task<bool> TryClaimForMaintenanceAsync(
        int assetId,
        Guid employeeId,
        CancellationToken ct = default)
    {
        var affected = await GetAllAvailableAssetForMaintenance(employeeId)
            .Where(a => a.Id == assetId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(
                    a => a.Status,
                    (byte)AssetStatusEnum.UnderMaintenance),
                ct);

        return affected == 1;
    }

    public async Task<bool> ReleaseFromMaintenanceAsync(
        int assetId,
        CancellationToken ct = default)
    {
        var affected = await Set
            .Where(a =>
                a.Id == assetId &&
                a.Status == (byte)AssetStatusEnum.UnderMaintenance)
            .ExecuteUpdateAsync(
                s => s.SetProperty(
                    a => a.Status,
                    a => a.EmployeeId == null
                        ? (byte)AssetStatusEnum.Available
                        : (byte)AssetStatusEnum.InUse),
                ct);

        return affected == 1;
    }

    public async Task<bool> RetireAsync(
        int assetId,
        AssetStatusEnum newStatus,
        CancellationToken ct = default)
    {
        if (newStatus is not (
            AssetStatusEnum.Disposed or
            AssetStatusEnum.Archived))
        {
            return false;
        }

        var affected = await Set
            .Where(a =>
                a.Id == assetId &&
                a.Status == (byte)AssetStatusEnum.UnderMaintenance)
            .ExecuteUpdateAsync(
                s => s.SetProperty(
                    a => a.Status,
                    (byte)newStatus),
                ct);

        return affected == 1;
    }

    public async Task<bool> TryAssignReplacementAsync(
        int assetId,
        Guid? ownerId,
        CancellationToken ct = default)
    {
        var affected = await Set
            .Where(a =>
                a.Id == assetId &&
                a.EmployeeId == null &&
                a.Status == (byte)AssetStatusEnum.Available)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(
                        a => a.Status,
                        (byte)AssetStatusEnum.InUse)
                    .SetProperty(
                        a => a.EmployeeId,
                        ownerId),
                ct);

        return affected == 1;
    }
}
