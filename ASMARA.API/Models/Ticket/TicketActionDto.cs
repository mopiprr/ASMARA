
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

public record ResubmitProcurementTicketRequestDto(
    string Subject,
    string? Description,
    List<CreateProcurementItemRequest> Items,
    string? Note = null
);

public record CreateProcurementItemRequest(
    int ItemCategoryId,
    string ItemName,
    int Quantity,
    decimal Price,
    int? ItemId = null
);

public record TicketNoteRequestDto(string? Note = "");

public record ReceiveItemNoteRequestDto(string? Note = "");

public record TicketApprovalDto(
    string Action,
    string? Note = null
);

public record AssignTechnicianDto(
    Guid TechnicianId,
    string? InstructionNote = null
);

public record PartCheckRequestDto(
    bool Available,
    string? Note = null
);

public record ReplacementRequestDto(
    bool Available,
    int? ReplacementAssetId = null,
    string? Note = null,
    string? Subject = null,
    string? Description = null,
    List<CreateProcurementItemRequest>? Items = null
);

public record OrderPartItemRequestDto(
    int PartIndex,
    int AvailableQuantity,
    decimal EstimatedPrice
);

public record OrderPartRequestDto(
    string Subject,
    string? Description,
    List<CreateProcurementItemRequest> Items,
    string? Note = null
);

public record SendReplacementRequestDto(
    int ReplacementAssetId,
    string? Note = null
);

public class ReceiveGoodsRequestDto
{
    public string? Note { get; set; }
    public IFormFile? ProofFile { get; set; }
}
