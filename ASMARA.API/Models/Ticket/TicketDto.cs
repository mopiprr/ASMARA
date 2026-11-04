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

public record CreateTicketProcurementDto(
    string Subject,
    List<CreateProcurementItemDto> Items,
    string? Description = null,
    Guid? ParentTicketId = null
);

public record CreateProcurementItemDto(
    int? ItemCategoryId = null,
    string? ItemName = null,
    int Quantity = 1,
    decimal Price = 0,
    int? ItemId = null
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

public record TicketListItemDto(
    Guid Id,
    string TicketNumber,
    string Subject,
    DateTime CreatedAt,
    string CurrentStep,
    string CurrentStepName);