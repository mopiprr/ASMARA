namespace ASMARA.API.Models.Ticket;

public class MaintenanceDetailDto
{
    
}
    
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