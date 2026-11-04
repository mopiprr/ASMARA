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
        List<ProcurementDetailResponseDto>? ProcurementDetails = null
        );