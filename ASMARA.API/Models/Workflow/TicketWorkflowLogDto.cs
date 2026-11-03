namespace ASMARA.API.Models.Workflow;

public class TicketWorkflowLogDto
{
    
}

public record CreateTicketWorkflowLogDto(
    Guid TicketId,
    string FromStep,
    string ToStep,
    Guid EmployeeId,
    string EmployeeName,
    string? Note = null
);

public record TicketWorkflowLogResponseDto(
    int Id,
    Guid TicketId,
    string FromStep,
    string ToStep,
    Guid EmployeeId,
    string EmployeeName,
    string? Note,
    DateTime CreatedAt
);