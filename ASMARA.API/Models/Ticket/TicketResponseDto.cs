using ASMARA.API.Models.Ticket;

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
    bool CanApprove = false,
    bool CanRevise = false,
    bool CanReject = false,
    bool CanReceiveGoods = false,
    bool CanResubmit = false
);

public record TicketWorkflowHistoryDto(
    string FromStep,
    string ToStep,
    Guid EmployeeId,
    string? EmployeeName,
    string? Note,
    DateTime CreatedAt
);