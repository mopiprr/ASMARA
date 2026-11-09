namespace ASMARA.API.Models.Inventory;

public class ItemCategoryDto
{
    
}
    
    public record ItemCategoryResponseDto(
        int Id,
        string CategoryCode,
        string CategoryName,
        byte TrackingType,
        bool IsActive
        );