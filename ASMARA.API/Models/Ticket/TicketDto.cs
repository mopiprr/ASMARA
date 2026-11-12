
using ASMARA.API.Enums;

namespace ASMARA.API.Models.Ticket;

public class TicketDto
{
}

public record TicketResponseDto(
    Guid Id,
    Guid? ParentTicketId,
    Guid EmployeeId,
    string? EmployeeName,
    string TicketNumber,
    byte TicketType,
    string Subject,
    string? Description,
    Guid? AssignedToEmployeeId,
    string? AssignedTo,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    DateTime? UpdatedAt,
    Guid? CurrentTaskEmployeeId,
    string CurrentStep,
    MaintenanceDetailResponseDto? MaintenanceDetail = null,
    List<ProcurementDetailResponseDto>? ProcurementDetails = null,
    List<TicketWorkflowHistoryDto>? History = null,

    // Existing workflow permissions.
    bool CanApprove = false,
    bool CanRevise = false,
    bool CanReject = false,
    bool CanReceiveGoods = false,
    bool CanResubmit = false,
    bool CanAssignHandler = false,
    bool CanSendItem = false,
    bool CanReceiveItem = false,

    // Existing procurement property.
    string? PurchaseProofFileName = null,

    // Maintenance Handler permissions.
    bool CanSubmitRepairResult = false,
    bool CanReceivePart = false,

    // Inventory permissions.
    bool CanCheckPart = false,
    bool CanOrderPart = false,
    bool CanSendPart = false,
    bool CanProcessReplacement = false,
    bool CanSendReplacement = false,

    // IT Support permission.
    bool CanReviewRepairResult = false,

    // Division Manager - Maintenance disposition permissions.
    bool CanArchive = false,
    bool CanDispose = false
);

public record TicketWorkflowHistoryDto(
    string? Action,
    string ActionLabel,
    string FromStep,
    string ToStep,
    Guid EmployeeId,
    string? EmployeeName,
    string? Note,
    DateTime CreatedAt
);

public record TicketListItemDto(
    Guid Id,
    string TicketNumber,
    string Subject,
    DateTime CreatedAt,
    string CurrentStep,
    byte TicketType,
    string CurrentStepName
);

public record PendingTicketListItemDto(
    Guid Id,
    string TicketNumber,
    byte TicketType,
    string TicketTypeName,
    string Subject,
    Guid RequesterId,
    string RequesterName,
    DateTime CreatedAt,
    string CurrentStep,
    string CurrentStepName
);

public record MyTicketStatisticsDto(
    int TotalRequests,
    int ActiveRequests,
    int ClosedRequests,
    int RejectedRequests
);

// Maintenance: daftar spare part yang diminta Handler.
public record MaintenancePartItemRequestDto(
    int? ItemId,
    int ItemCategoryId,
    string ItemName,
    int Quantity
);

// Maintenance: hasil pemeriksaan/perbaikan dari Handler.
public record SubmitRepairResultRequestDto(
    RepairResultEnum Result,
    string? HandlerNote = null,
    string? PartRequirement = null,
    List<MaintenancePartItemRequestDto>? Parts = null
);
