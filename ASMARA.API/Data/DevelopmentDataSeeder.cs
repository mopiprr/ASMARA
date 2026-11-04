
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(AsmaraDbContext db)
    {
        // Ambil karyawan yang sudah tersedia.
        var employees = await db.Employees
            .OrderBy(e => e.EmployeeNumber)
            .Take(5)
            .ToListAsync();

        if (employees.Count < 4)
        {
            throw new InvalidOperationException(
                "Minimal 4 data Employee diperlukan untuk development seed."
            );
        }

        // 1. Seed ItemCategory.
        var categoryDefinitions = new[]
        {
            new { Code = "DEV-CAT-001", Name = "Computer & Laptop" },
            new { Code = "DEV-CAT-002", Name = "Monitor" },
            new { Code = "DEV-CAT-003", Name = "Printer" },
            new { Code = "DEV-CAT-004", Name = "Network Equipment" },
            new { Code = "DEV-CAT-005", Name = "Presentation Equipment" }
        };

        foreach (var definition in categoryDefinitions)
        {
            var exists = await db.ItemCategories
                .AnyAsync(x => x.CategoryCode == definition.Code);

            if (!exists)
            {
                db.ItemCategories.Add(new ItemCategory
                {
                    CategoryCode = definition.Code,
                    CategoryName = definition.Name,
                    TrackingType = (byte)TrackingTypeEnum.NonConsumable,
                    IsActive = true
                });
            }
        }

        await db.SaveChangesAsync();

        // Ambil kategori berdasarkan kode.
        var categories = await db.ItemCategories
            .Where(x => categoryDefinitions
                .Select(c => c.Code)
                .Contains(x.CategoryCode))
            .ToDictionaryAsync(x => x.CategoryCode);

        // 2. Seed ItemInventory.
        var itemDefinitions = new[]
        {
            new
            {
                Code = "DEV-ITEM-001",
                Name = "Lenovo ThinkPad Laptop",
                CategoryCode = "DEV-CAT-001"
            },
            new
            {
                Code = "DEV-ITEM-002",
                Name = "Dell Monitor 24 Inch",
                CategoryCode = "DEV-CAT-002"
            },
            new
            {
                Code = "DEV-ITEM-003",
                Name = "HP LaserJet Printer",
                CategoryCode = "DEV-CAT-003"
            },
            new
            {
                Code = "DEV-ITEM-004",
                Name = "TP-Link Network Router",
                CategoryCode = "DEV-CAT-004"
            },
            new
            {
                Code = "DEV-ITEM-005",
                Name = "Epson Projector",
                CategoryCode = "DEV-CAT-005"
            }
        };

        foreach (var definition in itemDefinitions)
        {
            var exists = await db.ItemInventories
                .AnyAsync(x => x.ItemCode == definition.Code);

            if (!exists)
            {
                db.ItemInventories.Add(new ItemInventory
                {
                    ItemCategoryId = categories[definition.CategoryCode].Id,
                    ItemCode = definition.Code,
                    ItemName = definition.Name,
                    IsActive = true,
                    Qty = null
                });
            }
        }

        await db.SaveChangesAsync();

        // Ambil item berdasarkan kode.
        var items = await db.ItemInventories
            .Where(x => itemDefinitions
                .Select(i => i.Code)
                .Contains(x.ItemCode))
            .ToDictionaryAsync(x => x.ItemCode);

        // 3. Seed Asset.
        var assetDefinitions = new[]
        {
            new
            {
                Tag = "DEV-ASM-0001",
                ItemCode = "DEV-ITEM-001",
                Status = AssetStatusEnum.InUse,
                EmployeeId = (Guid?)employees[0].Id
            },
            new
            {
                Tag = "DEV-ASM-0002",
                ItemCode = "DEV-ITEM-001",
                Status = AssetStatusEnum.InUse,
                EmployeeId = (Guid?)employees[1].Id
            },
            new
            {
                Tag = "DEV-ASM-0003",
                ItemCode = "DEV-ITEM-002",
                Status = AssetStatusEnum.InUse,
                EmployeeId = (Guid?)employees[2].Id
            },
            new
            {
                Tag = "DEV-ASM-0004",
                ItemCode = "DEV-ITEM-003",
                Status = AssetStatusEnum.UnderMaintenance,
                EmployeeId = (Guid?)employees[3].Id
            },
            new
            {
                Tag = "DEV-ASM-0005",
                ItemCode = "DEV-ITEM-004",
                Status = AssetStatusEnum.Available,
                EmployeeId = (Guid?)null
            },
            new
            {
                Tag = "DEV-ASM-0006",
                ItemCode = "DEV-ITEM-001",
                Status = AssetStatusEnum.Disposed,
                EmployeeId = (Guid?)null
            },
            new
            {
                Tag = "DEV-ASM-0007",
                ItemCode = "DEV-ITEM-005",
                Status = AssetStatusEnum.Archived,
                EmployeeId = (Guid?)null
            }
        };

        foreach (var definition in assetDefinitions)
        {
            var exists = await db.Assets
                .AnyAsync(x => x.AssetTag == definition.Tag);

            if (!exists)
            {
                db.Assets.Add(new Asset
                {
                    AssetTag = definition.Tag,
                    ItemId = items[definition.ItemCode].Id,
                    EmployeeId = definition.EmployeeId,
                    Status = (byte)definition.Status
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
