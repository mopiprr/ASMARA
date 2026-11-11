using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Queries.GetInventoryItemAssets;

public record GetInventoryItemAssetsQuery(int ItemId) : IRequest<List<InventoryItemAssetDto>>;

public class GetInventoryItemAssetsQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetInventoryItemAssetsQuery, List<InventoryItemAssetDto>>
{
    public async Task<List<InventoryItemAssetDto>> Handle(GetInventoryItemAssetsQuery request, CancellationToken ct)
    {
        var availableStatus = (byte)AssetStatusEnum.Available;

        return await uow.Assets.QueryNoTracking()
            .Include(a => a.Employee)
                .ThenInclude(e => e!.Department)
            .Where(a => a.ItemId == request.ItemId &&
                        a.Status != (byte)AssetStatusEnum.Disposed &&
                        a.Status != (byte)AssetStatusEnum.Archived)
            .OrderBy(a => a.AssetTag)
            .Select(a => new InventoryItemAssetDto(
                a.Id,
                a.AssetTag,
                a.Status,
                ((AssetStatusEnum)a.Status).ToString(),
                a.EmployeeId,
                a.Employee != null ? a.Employee.FullName : null,
                a.Employee != null && a.Employee.Department != null ? a.Employee.Department.DepartmentName : null,
                a.Status == availableStatus && a.EmployeeId == null
            ))
            .ToListAsync(ct);
    }
}
