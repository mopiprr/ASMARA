using ASMARA.API.Common.Security;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Queries.GetAssetsForMaintenance;

public record GetAssetsForMaintenanceQuery : IRequest<IReadOnlyCollection<MaintenanceAssetOptionDto>>;

public class GetAssetsForMaintenanceQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    : IRequestHandler<GetAssetsForMaintenanceQuery, IReadOnlyCollection<MaintenanceAssetOptionDto>>
{
    public async Task<IReadOnlyCollection<MaintenanceAssetOptionDto>> Handle(GetAssetsForMaintenanceQuery request, CancellationToken cancellationToken)
        => await uow.Assets.GetAllAvailableAssetForMaintenance(currentUser.EmployeeId)   
            .AsNoTracking()
            .OrderBy(a => a.EmployeeId == null)   
            .ThenBy(a => a.AssetTag)
            .Select(a => new MaintenanceAssetOptionDto(
                a.Id,
                a.AssetTag,
                a.Item.ItemName,
                a.Item.ItemCategory.CategoryName,
                a.EmployeeId == null))
            .ToListAsync(cancellationToken);
}
