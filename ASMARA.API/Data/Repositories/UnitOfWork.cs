using ASMARA.API.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data.Repositories;

public class UnitOfWork(AsmaraDbContext context) : IUnitOfWork
{
    private IAccountRepository? _accounts;
    private IAssetRepository? _assets;
    private IDepartmentRepository? _departments;
    private IEmployeeRepository? _employees;
    private IItemCategoryRepository? _itemCategories;
    private IItemInventoryRepository? _itemInventories;
    private IMaintenanceDetailRepository? _maintenanceDetails;
    private IProcurementDetailRepository? _procurementDetails;
    private IRoleRepository? _roles;
    private ITicketRepository? _tickets;
    private ITicketWorkflowLogRepository? _ticketWorkflowLogs;
    private IWorkflowRepository? _workflows;
    private IWorkflowStepRepository? _workflowSteps;
    private IDepartmentBudgetRepository? _departmentBudgets;
    private IBudgetLogRepository? _budgetLogs;

    public IAccountRepository Accounts => _accounts ??= new AccountRepository(context);
    public IAssetRepository Assets => _assets ??= new AssetRepository(context);
    public IDepartmentRepository Departments => _departments ??= new DepartmentRepository(context);
    public IEmployeeRepository Employees => _employees ??= new EmployeeRepository(context);
    public IItemCategoryRepository ItemCategories => _itemCategories ??= new ItemCategoryRepository(context);
    public IItemInventoryRepository ItemInventories => _itemInventories ??= new ItemInventoryRepository(context);
    public IMaintenanceDetailRepository MaintenanceDetails => _maintenanceDetails ??= new MaintenanceDetailRepository(context);
    public IProcurementDetailRepository ProcurementDetails => _procurementDetails ??= new ProcurementDetailRepository(context);
    public IRoleRepository Roles => _roles ??= new RoleRepository(context);
    public ITicketRepository Tickets => _tickets ??= new TicketRepository(context);
    public ITicketWorkflowLogRepository TicketWorkflowLogs => _ticketWorkflowLogs ??= new TicketWorkflowLogRepository(context);
    public IWorkflowRepository Workflows => _workflows ??= new WorkflowRepository(context);
    public IWorkflowStepRepository WorkflowSteps => _workflowSteps ??= new WorkflowStepRepository(context);
    public IDepartmentBudgetRepository DepartmentBudgets => _departmentBudgets ??= new DepartmentBudgetRepository(context);
    public IBudgetLogRepository BudgetLogs => _budgetLogs ??= new BudgetLogRepository(context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This ticket has just been processed by someone else. Please refresh and try again.");
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        var result = await action(ct); 
        await tx.CommitAsync(ct);
        return result;
    }
}
