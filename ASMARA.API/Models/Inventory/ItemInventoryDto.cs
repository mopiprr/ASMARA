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
