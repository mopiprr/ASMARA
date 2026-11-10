namespace ASMARA.API.Models.Inventory;

public class AssetDto
{
    
}

public record MaintenanceAssetOptionDto(
    int AssetId,
    string AssetTag,
    string ItemName,
    string CategoryName,
    bool IsShared);

public record AssetListItemDto(
    int Id,
    string AssetTag,
    string ItemName,
    string CategoryName,
    byte Status,
    Guid? HolderId,
    string? HolderName,
    string? HolderDepartment,
    bool IsPublic);

public record AssetStatisticsDto(
    int TotalAssets,
    int Available,
    int InUse,
    int UnderMaintenance);

public record AssignAssetRequestDto(Guid? EmployeeId);