namespace ASMARA.API.Models.Ticket;

public class TicketActionDto
{
    
}

public record CreateMaintenanceTicketRequestDto(
    string Subject,
    string? Description,
    int AssetId,
    string ProblemDescription
    );
    
public record CreateProcurementTicketRequestDto(
    string Subject,
    string? Description,
    List<CreateProcurementItemRequest> Items,
    Guid? ParentTicketId = null
    );
    
public record CreateProcurementItemRequest(
    int ItemCategoryId,
    string ItemName,
    int Quantity,
    decimal Price,
    int? ItemId = null
    );
    
public record TicketApprovalDto(
    string Action,
    string? Note
    );
        
public record AssignTechnicianDto(
    Guid TechnicianId,
    string? InstructionNote = null
    );
    
public record SubmitRepairResultDto(
    int ReplacementAssetId,
    string? Note = null
    );
        
public record ReceiveInventoryItemDto(
    int ItemInventoryId,
    int Quantity, 
    string? Note = null
    );
            
public record CloseTicketDto(
    string? FeedbackNote = null
    );