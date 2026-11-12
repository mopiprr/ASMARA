using ASMARA.API.Entities;
using ASMARA.API.Enums;

namespace ASMARA.API.Data.Repositories;

public interface IAssetRepository : IRepository<Asset>
{
    IQueryable<Asset> GetAllAvailableAssetForMaintenance(Guid employeeId);

    Task<bool> TryClaimForMaintenanceAsync(int assetId, Guid employeeId, CancellationToken ct = default);
    Task<bool> ReleaseFromMaintenanceAsync(int assetId, CancellationToken ct = default);
    Task<bool> RetireAsync(int assetId, AssetStatusEnum newStatus, CancellationToken ct = default);
    Task<bool> TryAssignReplacementAsync(int assetId, Guid? ownerId, CancellationToken ct = default);
    Task<bool> TryAssignToEmployeeAsync(int assetId, Guid? employeeId, CancellationToken ct = default);
}
