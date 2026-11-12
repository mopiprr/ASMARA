using ASMARA.API.Common.Exceptions;
using ASMARA.API.Data.Repositories;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using ASMARA.API.Models.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Features.Catalog.Commands.CreateInventoryItem;

public record CreateInventoryItemCommand(
    int ItemCategoryId,
    string ItemName,
    int InitialStock = 0
) : IRequest<ItemInventoryResponseDto>;

public class CreateInventoryItemCommandHandler(IUnitOfWork uow)
    : IRequestHandler<CreateInventoryItemCommand, ItemInventoryResponseDto>
{
    public async Task<ItemInventoryResponseDto> Handle(CreateInventoryItemCommand request, CancellationToken ct)
    {
        // 1. Validasi keberadaan kategori barang
        var category = await uow.ItemCategories.GetByIdAsync(request.ItemCategoryId, ct);
        if (category is null || !category.IsActive)
        {
            throw new NotFoundException($"Item category with ID {request.ItemCategoryId} does not exist or is inactive.");
        }

        // 2. Generate ItemCode otomatis (misal CAT-PRJ -> PRJ-001)
        var catPrefix = category.CategoryCode.Replace("CAT-", "").Replace("DEV-CAT-", "").Trim();
        if (string.IsNullOrWhiteSpace(catPrefix)) catPrefix = "ITM";
        if (catPrefix.Length > 4) catPrefix = catPrefix[..4];

        var countSameCat = await uow.ItemInventories.QueryNoTracking()
            .CountAsync(i => i.ItemCategoryId == category.Id, ct);

        var candidateCode = $"{catPrefix}-{(countSameCat + 1):D3}";
        while (await uow.ItemInventories.QueryNoTracking().AnyAsync(i => i.ItemCode == candidateCode, ct))
        {
            countSameCat++;
            candidateCode = $"{catPrefix}-{(countSameCat + 1):D3}";
        }

        var isNonConsumable = category.TrackingType == (byte)TrackingTypeEnum.NonConsumable;
        var initialStock = Math.Max(0, request.InitialStock);

        // 3. Eksekusi penyimpanan (transaksional untuk non-consumable yang generate asset tags)
        return await uow.ExecuteInTransactionAsync(async txCt =>
        {
            var newItem = new ItemInventory
            {
                ItemCategoryId = category.Id,
                ItemCode = candidateCode,
                ItemName = request.ItemName.Trim(),
                IsActive = true,
                Qty = isNonConsumable ? initialStock : initialStock
            };

            uow.ItemInventories.Add(newItem);
            await uow.SaveChangesAsync(txCt);

            int totalAssets = 0;
            int availableAssets = 0;

            // 4. Jika NonConsumable: Otomatis generate N aset dengan AssetTag unik
            if (isNonConsumable && initialStock > 0)
            {
                var existingTags = await uow.Assets.QueryNoTracking()
                    .Where(a => a.AssetTag.StartsWith("AST-"))
                    .Select(a => a.AssetTag)
                    .ToListAsync(txCt);

                var currentMaxTagNum = existingTags
                    .Select(tag => int.TryParse(tag.Replace("AST-", ""), out var num) ? num : 0)
                    .DefaultIfEmpty(0)
                    .Max();

                for (var i = 0; i < initialStock; i++)
                {
                    currentMaxTagNum++;
                    var newAsset = new Asset
                    {
                        ItemId = newItem.Id,
                        AssetTag = $"AST-{currentMaxTagNum:D6}",
                        Status = (byte)AssetStatusEnum.Available,
                        EmployeeId = null // Tersedia di inventaris gudang
                    };
                    uow.Assets.Add(newAsset);
                }

                await uow.SaveChangesAsync(txCt);
                totalAssets = initialStock;
                availableAssets = initialStock;
            }

            return new ItemInventoryResponseDto(
                newItem.Id,
                newItem.ItemCategoryId,
                category.CategoryName,
                newItem.ItemCode,
                newItem.ItemName,
                isNonConsumable ? availableAssets : newItem.Qty,
                newItem.IsActive,
                category.TrackingType,
                isNonConsumable ? totalAssets : null,
                isNonConsumable ? availableAssets : null
            );
        }, ct);
    }
}
