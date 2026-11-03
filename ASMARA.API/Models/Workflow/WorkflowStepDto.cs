namespace ASMARA.API.Models.Workflow;

public class WorkflowStepDto
{
    
}

public record CreateWorkflowStepDto(
    int WorkflowId,
    string StepCode,
    string Name,
    int? Level = null
    );
    
    public record UpdateWorkflowStepDto(
        string StepCode,
        string Name,
        int? Level = null
    );
    
    public record WorkflowStepResponseDto(
        int Id,
        int WorkflowId,
        string StepCode,
        string Name,
        int? Level
    );