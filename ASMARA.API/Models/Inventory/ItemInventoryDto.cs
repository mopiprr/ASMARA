namespace ASMARA.API.Models.Inventory;

public class ItemInventoryDto { }

public record ItemInventoryResponseDto(
    int Id,
    int ItemCategoryId,
    string? CategoryName,
    string ItemCode,
    string ItemName,
    int? Qty,
    bool IsActive,
    byte? TrackingType = 0,
    int? TotalAssetCount = null,
    int? AvailableAssetCount = null
);

public record CreateInventoryItemRequestDto(
    int ItemCategoryId,
    string ItemName,
    int InitialStock = 0
);

public record UpdateInventoryItemRequestDto(
    string ItemName,
    int? ItemCategoryId = null,
    int? NewStock = null,
    bool? IsActive = null
);

public record InventoryItemAssetDto(
    int Id,
    string AssetTag,
    byte Status,
    string StatusName,
    Guid? EmployeeId,
    string? EmployeeName,
    string? DepartmentName,
    bool CanDelete
);

public record DeleteInventoryUnitsRequestDto(
    int? Quantity,
    List<int>? AssetIds,
    string Reason,
    string? Notes = null,
    bool DeactivateIfZero = false
);

public record DeleteInventoryUnitsResultDto(
    int ItemId,
    string ItemName,
    int DeletedCount,
    int? RemainingStock,
    bool IsActive,
    string Message
);

