namespace ASMARA.API.Models.Inventory;

public class ItemCategoryDto
{
    
}

public record CreateItemCategoryRequest(
    string CategoryCode,
    string CategoryName,
    string TrackingType,
    bool IsActive = true
    );
    
    public record UpdateItemCategoryRequest(
        string CategoryName,
        byte TrackingType,
        bool IsActive = true
    );
    
    public record ItemCategoryResponseDto(
        int Id,
        string CategoryCode,
        string CategoryName,
        byte TrackingType,
        bool IsActive
        );