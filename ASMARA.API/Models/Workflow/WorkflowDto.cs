namespace ASMARA.API.Models.Workflow;

public class WorkflowDto
{
    
}

public record CreateWorkflowDto(
    string WorkflowCode,
    string WorkflowName,
    byte TicketType,
    string? Description = null,
    bool IsActive = true
    );
    
    public record UpdateWorkflowDto(
        string WorkflowName,
        byte TicketType,
        string? Description = null,
        bool IsActive = true
    );
    
    public record WorkflowResponseDto(
        int Id,
        string WorkflowCode,
        string WorkflowName,
        byte TicketType,
        string? Description,
        bool IsActive,
        DateTime CreatedAt,
        List<WorkflowStepResponseDto>? Steps = null
        );