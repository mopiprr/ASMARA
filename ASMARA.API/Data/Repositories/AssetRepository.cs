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
}