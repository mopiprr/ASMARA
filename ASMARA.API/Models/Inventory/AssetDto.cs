namespace ASMARA.API.Models.Inventory;

public class AssetDto
{
    
}

public record CreateAssetDto(
    int ItemId,
    string AssetTag,
    byte Status,
    Guid? EmployeeId = null
    );
    
    public record UpdateAssetDto(
        byte Status,
        Guid? EmployeeId = null
    );
    
    public record AssetResponseDto(
        int Id,
        int ItemId,
        string? ItemName,
        Guid? EmployeeId,
        string? EmployeeName,
        string AssetTag,
        byte Status
    );

public record MaintenanceAssetOptionDto(
    int AssetId,
    string AssetTag,
    string ItemName,
    string CategoryName,
    bool IsShared);