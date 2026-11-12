namespace ASMARA.API.Models.Finance
{
    public class FinanceDto
    {

    }

    public record DepartmentBudgetDto(
        Guid Id,
        Guid DepartmentId,
        string DepartmentCode,
        string DepartmentName,
        int FiscalYear,
        decimal AllocatedBudget,
        decimal SpentBudget,
        decimal CommittedBudget,
        decimal RemainingBudget,
        decimal UtilizationRate,
        string Status,
        DateTime CreatedAt,
        DateTime? UpdatedAt
        );

    public record BudgetOverfiewDto(
        int FiscalYear,
        decimal TotalAllocated,
        decimal TotalSpent,
        decimal TotalCommitted,
        decimal TotalRemaining,
        decimal OverallUtilizationRate
        );

    public record BudgetCheckResultDto(
        string DepartmentName,
        decimal EstimatedTotal,
        decimal RemainingBudget,
        bool IsSufficient,
        decimal ProjectUtilizationRate,
        string Status,
        string Message
        );

    public record BudgetLogDto(
        Guid Id,
        string LogNumber,
        Guid DepartmentBudgetId,
        string DepartmentName,
        Guid? TicketId,
        string? TicketNumber,
        Guid? RelatedDepartmentId,
        string? RelatedDepartmentName,
        byte ChangeType,
        string ChangeTypeName,
        decimal Amount,
        decimal PreviousAllocated,
        decimal NewAllocated,
        decimal PreviousRemaining,
        decimal NewRemaining,
        string Description,
        string CreatedByName,
        DateTime CreatedAt
    );

    public record AdjustBudgetRequestDto(
        Guid DepartmentId,
        Guid? TargetDepartmentId,
        byte ChangeType,
        decimal Amount,
        string Reason
    );
}
