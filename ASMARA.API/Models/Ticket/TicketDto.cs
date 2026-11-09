namespace ASMARA.API.Models.Ticket;

public class TicketDto
{
    
}

public record CreateTicketDto(
    Guid EmployeeId,
    byte TicketType,
    string Subject,
    string? Description = null,
    Guid? ParentTicketId = null,
    Guid? AssignedToEmployeeId = null,
    string? AssignedTo = null
    );

public record CompletedTicketListDto(
    Guid Id,
    string TicketNumber,
    string TicketType,
    string Subject,
    Guid RequesterId,
    string RequesterName,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    string CurrentStep,
    string CurrentStepName);
    
    public record UpdateTicketDto(
        string Subject,
        string? Description = null,
        Guid? AssignedToEmployeeId = null,
        string? AssignedTo = null,
        Guid? CurrentTaskEmployeeId = null
    );
    
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
    string CurrentStepName);

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
    string CurrentStepName);

public record MyTicketStatisticsDto(
    int TotalRequests,
    int ActiveRequests,
    int ClosedRequests,
    int RejectedRequests);