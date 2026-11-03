namespace ASMARA.API.Models.Workflow;

public class TicketWorkflowLogDto
{
    
}

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