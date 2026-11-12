using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Commands.UpdateInventoryItem;

public record UpdateInventoryItemCommand(
    int Id,
    string ItemName,
    int? ItemCategoryId = null,
    int? NewStock = null,
    bool? IsActive = null
) : IRequest<ItemInventoryResponseDto>;

public class UpdateInventoryItemCommandHandler(IUnitOfWork uow)
    : IRequestHandler<UpdateInventoryItemCommand, ItemInventoryResponseDto>
{
    public async Task<ItemInventoryResponseDto> Handle(UpdateInventoryItemCommand request, CancellationToken ct)
    {
        var item = await uow.ItemInventories.Query()
            .Include(i => i.ItemCategory)
            .Include(i => i.Assets)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);

        if (item is null)
        {
            throw new NotFoundException($"Inventory item with ID {request.Id} was not found.");
        }

        return await uow.ExecuteInTransactionAsync(async txCt =>
        {
            // 1. Update nama barang
            if (!string.IsNullOrWhiteSpace(request.ItemName))
            {
                item.ItemName = request.ItemName.Trim();
            }

            // 2. Update status aktif (jika ada)
            if (request.IsActive.HasValue)
            {
                if (!request.IsActive.Value)
                {
                    var hasInUseAssets = item.Assets.Any(a =>
                        a.Status == (byte)AssetStatusEnum.InUse || a.EmployeeId != null);

                    if (hasInUseAssets)
                    {
                        throw new BusinessRuleException(
                            "Cannot deactivate this item because some units are currently assigned to employees or in use.");
                    }
                }

                item.IsActive = request.IsActive.Value;
            }

            // 3. Update kategori barang (jika ada perubahan)
            if (request.ItemCategoryId.HasValue && request.ItemCategoryId.Value != item.ItemCategoryId)
            {
                var newCategory = await uow.ItemCategories.GetByIdAsync(request.ItemCategoryId.Value, txCt);
                if (newCategory is null || !newCategory.IsActive)
                {
                    throw new NotFoundException($"Target item category with ID {request.ItemCategoryId.Value} was not found or is inactive.");
                }

                // Cek apakah ada perubahan tracking type
                if (item.ItemCategory.TrackingType != newCategory.TrackingType)
                {
                    if (item.ItemCategory.TrackingType == (byte)TrackingTypeEnum.NonConsumable && item.Assets.Any())
                    {
                        throw new BusinessRuleException(
                            "Cannot change category to Consumable because individual serialized assets are already tracked for this item.");
                    }
                }

                item.ItemCategoryId = newCategory.Id;
                item.ItemCategory = newCategory;
            }

            var category = item.ItemCategory;
            var isNonConsumable = category.TrackingType == (byte)TrackingTypeEnum.NonConsumable;

            // 4. Update stok (jika ada)
            if (request.NewStock.HasValue)
            {
                var targetStock = Math.Max(0, request.NewStock.Value);

                if (!isNonConsumable)
                {
                    // Consumable: Langsung update nilai Qty
                    item.Qty = targetStock;
                }
                else
                {
                    // Non-Consumable: Sinkronisasi dengan entri di tabel Asset
                    var currentTotalAssets = item.Assets.Count;

                    if (targetStock > currentTotalAssets)
                    {
                        // Penambahan stok: Generate unit Asset baru ber-AssetTag
                        var diff = targetStock - currentTotalAssets;

                        var existingTags = await uow.Assets.QueryNoTracking()
                            .Where(a => a.AssetTag.StartsWith("AST-"))
                            .Select(a => a.AssetTag)
                            .ToListAsync(txCt);

                        var currentMaxTagNum = existingTags
                            .Select(tag => int.TryParse(tag.Replace("AST-", ""), out var num) ? num : 0)
                            .DefaultIfEmpty(0)
                            .Max();

                        for (var i = 0; i < diff; i++)
                        {
                            currentMaxTagNum++;
                            var newAsset = new Asset
                            {
                                ItemId = item.Id,
                                AssetTag = $"AST-{currentMaxTagNum:D6}",
                                Status = (byte)AssetStatusEnum.Available,
                                EmployeeId = null
                            };
                            uow.Assets.Add(newAsset);
                            item.Assets.Add(newAsset);
                        }
                    }
                    else if (targetStock < currentTotalAssets)
                    {
                        // Pengurangan stok: Hanya boleh menghapus aset yang masih Available & belum dipegang karyawan
                        var diff = currentTotalAssets - targetStock;
                        var availableAssets = item.Assets
                            .Where(a => a.Status == (byte)AssetStatusEnum.Available && a.EmployeeId == null)
                            .Take(diff)
                            .ToList();

                        if (availableAssets.Count < diff)
                        {
                            throw new BusinessRuleException(
                                $"Cannot reduce stock to {targetStock}. Only {availableAssets.Count} units are available and unassigned in storage (other units are in use or under maintenance).");
                        }

                        foreach (var assetToRemove in availableAssets)
                        {
                            uow.Assets.Remove(assetToRemove);
                            item.Assets.Remove(assetToRemove);
                        }
                    }

                    item.Qty = targetStock;
                }
            }

            await uow.SaveChangesAsync(txCt);

            var availableCount = item.Assets.Count(a => a.Status == (byte)AssetStatusEnum.Available);
            var totalCount = item.Assets.Count;

            return new ItemInventoryResponseDto(
                item.Id,
                item.ItemCategoryId,
                item.ItemCategory.CategoryName,
                item.ItemCode,
                item.ItemName,
                isNonConsumable ? availableCount : item.Qty,
                item.IsActive,
                item.ItemCategory.TrackingType,
                isNonConsumable ? totalCount : null,
                isNonConsumable ? availableCount : null
            );
        }, ct);
    }
}
