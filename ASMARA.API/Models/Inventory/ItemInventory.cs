<<<<<<< Updated upstream
namespace ASMARA.API.Models.Inventory;

public class ItemInventory
{
    
}
    
    public record ItemInventoryResponseDto(
        int Id,
        int ItemCategoryId,
        string? CategoryName,
        string ItemCode,
        string ItemName,
        int? Qty,
        bool IsActive,
        byte TrackingType = 0,
        int? TotalAssetCount = null,
        int? AvailableAssetCount = null
    );
=======
namespace ASMARA.API.Models.Inventory;

public record ItemInventoryResponseDto(
    int Id,
    int ItemCategoryId,
    string? CategoryName,
    string ItemCode,
    string ItemName,
    int? Qty,
    bool IsActive,
    byte? TrackingType = null,
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
>>>>>>> Stashed changes
