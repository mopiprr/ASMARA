namespace ASMARA.API.Models.Ticket;

public class MaintenanceDetailDto
{
    
}

//public record CreateMaintenanceDetailDto(
//    Guid TicketId,
//    int AssetId,
//    string Description,
//    string? PartRequirement = null
//    );
    
public record UpdateMaintenanceDetailDto(
    byte? RepairResult = null,
    string? HandlerNote = null,
    string? PartRequirement = null,
    int? ReplacementAssetId = null
    );
    
public record MaintenanceDetailResponseDto(
    Guid TicketId,
    int AssetId,
    string? AssetTag,
    string Description,
    byte? RepairResult,
    string? HandlerNote,
    string? PartRequirement,
    int? ReplacementAssetId,
    string? ReplacementAssetTag,
    DateTime CreatedAt
    );