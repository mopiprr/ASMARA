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