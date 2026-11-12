using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Enums;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Commands.DeleteInventoryUnits;

public record DeleteInventoryUnitsCommand(
    int ItemId,
    DeleteInventoryUnitsRequestDto Dto
) : IRequest<DeleteInventoryUnitsResultDto>;

public class DeleteInventoryUnitsCommandHandler(IUnitOfWork uow)
    : IRequestHandler<DeleteInventoryUnitsCommand, DeleteInventoryUnitsResultDto>
{
    public async Task<DeleteInventoryUnitsResultDto> Handle(DeleteInventoryUnitsCommand request, CancellationToken ct)
    {
        var item = await uow.ItemInventories.Query()
            .Include(i => i.ItemCategory)
            .Include(i => i.Assets)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, ct);

        if (item is null)
        {
            throw new NotFoundException($"Inventory item with ID {request.ItemId} was not found.");
        }

        var isNonConsumable = item.ItemCategory.TrackingType == (byte)TrackingTypeEnum.NonConsumable;

        return await uow.ExecuteInTransactionAsync(async txCt =>
        {
            int deletedCount;

            if (!isNonConsumable)
            {
                // Consumable reduction
                if (!request.Dto.Quantity.HasValue || request.Dto.Quantity.Value <= 0)
                {
                    throw new BusinessRuleException("Please specify a valid quantity greater than 0 to delete/reduce.");
                }

                var qtyToDelete = request.Dto.Quantity.Value;
                if (qtyToDelete > item.Qty)
                {
                    throw new BusinessRuleException($"Cannot delete {qtyToDelete} units. Only {item.Qty} units are available in inventory.");
                }

                item.Qty -= qtyToDelete;
                deletedCount = qtyToDelete;

                if (request.Dto.DeactivateIfZero && item.Qty <= 0)
                {
                    item.IsActive = false;
                }
            }
            else
            {
                // NonConsumable specific serialized asset deletion
                if (request.Dto.AssetIds is null || request.Dto.AssetIds.Count == 0)
                {
                    throw new BusinessRuleException("Please select at least one asset to delete.");
                }

                var selectedAssets = item.Assets
                    .Where(a => request.Dto.AssetIds.Contains(a.Id))
                    .ToList();

                if (selectedAssets.Count != request.Dto.AssetIds.Count)
                {
                    throw new BusinessRuleException("Some of the selected assets were not found or do not belong to this item.");
                }

                // Check for assets in use or assigned to employees
                var inUseAsset = selectedAssets.FirstOrDefault(a =>
                    a.Status == (byte)AssetStatusEnum.InUse || a.EmployeeId != null);
                if (inUseAsset != null)
                {
                    throw new BusinessRuleException(
                        $"Asset {inUseAsset.AssetTag} cannot be deleted because it is currently assigned to an employee or in use.");
                }

                // Check for assets under maintenance
                var maintenanceAsset = selectedAssets.FirstOrDefault(a =>
                    a.Status == (byte)AssetStatusEnum.UnderMaintenance);
                if (maintenanceAsset != null)
                {
                    throw new BusinessRuleException(
                        $"Asset {maintenanceAsset.AssetTag} cannot be deleted because it is currently under maintenance.");
                }

                // Delete or dispose the selected assets
                foreach (var asset in selectedAssets)
                {
                    var hasMaintenanceHistory = await uow.MaintenanceDetails.QueryNoTracking()
                        .AnyAsync(m => m.AssetId == asset.Id || m.ReplacementAssetId == asset.Id, txCt);

                    if (hasMaintenanceHistory)
                    {
                        // Mark as Disposed to preserve historical foreign key integrity, but remove from active assets
                        asset.Status = (byte)AssetStatusEnum.Disposed;
                        asset.EmployeeId = null;
                    }
                    else
                    {
                        // Hard delete row from Asset table
                        uow.Assets.Remove(asset);
                        item.Assets.Remove(asset);
                    }
                }

                deletedCount = selectedAssets.Count;

                var remainingActiveAssets = item.Assets
                    .Count(a => a.Status != (byte)AssetStatusEnum.Disposed && a.Status != (byte)AssetStatusEnum.Archived);

                item.Qty = remainingActiveAssets;

                if (request.Dto.DeactivateIfZero && remainingActiveAssets <= 0)
                {
                    item.IsActive = false;
                }
            }

            await uow.SaveChangesAsync(txCt);

            var message = isNonConsumable
                ? $"Successfully deleted {deletedCount} asset unit(s) from {item.ItemName}."
                : $"Successfully reduced {deletedCount} unit(s) from {item.ItemName}.";

            return new DeleteInventoryUnitsResultDto(
                item.Id,
                item.ItemName,
                deletedCount,
                item.Qty,
                item.IsActive,
                message
            );
        }, ct);
    }
}
