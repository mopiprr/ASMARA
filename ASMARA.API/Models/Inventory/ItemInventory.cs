namespace ASMARA.API.Models.Inventory;

public class ItemInventory
{
    
}

public record CreateItemInventoryRequest(
    int ItemCategoryId,
    string ItemCode,
    string ItemName, 
    int? Qty = null,
    bool IsActive = true
    );
    
    public record UpdateItemInventoryRequest(
        int ItemCategoryId,
        string ItemName,
        int? Qty = null,
        bool IsActive = true
    );
    
    public record ItemInventoryResponseDto(
        int Id,
        int ItemCategoryId,
        string? CategoryName,
        string ItemCode,
        string ItemName,
        int? Qty,
        bool IsActive
    );