using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public interface IAssetRepository : IRepository<Asset>
{
    IQueryable<Asset> GetAllAvailableAssetForMaintenance(Guid employeeId);

    Task<bool> TryClaimForMaintenanceAsync(int assetId, Guid employeeId, CancellationToken ct = default);
}
