namespace ASMARA.API.Data.Repositories;

public interface IUnitOfWork
{
    IAccountRepository Accounts { get; }
    IAssetRepository Assets { get; }
    IDepartmentRepository Departments { get; }
    IEmployeeRepository Employees { get; }
    IItemCategoryRepository ItemCategories { get; }
    IItemInventoryRepository ItemInventories { get; }
    IMaintenanceDetailRepository MaintenanceDetails { get; }
    IProcurementDetailRepository ProcurementDetails { get; }
    IRoleRepository Roles { get; }
    ITicketRepository Tickets { get; }
    ITicketWorkflowLogRepository TicketWorkflowLogs { get; }
    IWorkflowRepository Workflows { get; }
    IWorkflowStepRepository WorkflowSteps { get; }
    IDepartmentBudgetRepository DepartmentBudgets { get; }
    IBudgetLogRepository BudgetLogs { get; }

    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
