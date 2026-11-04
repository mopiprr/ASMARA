
using Asmara.API.Data;
using ASMARA.API.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsmaraServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("AsmaraDB")
            ?? throw new InvalidOperationException(
                "Connection string 'AsmaraDB' tidak ditemukan.");

        // Database
        services.AddDbContext<AsmaraDbContext>(options =>
            options.UseSqlServer(connectionString));

        #region Services
        #endregion

        #region Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IItemCategoryRepository, ItemCategoryRepository>();
        services.AddScoped<IItemInventoryRepository, ItemInventoryRepository>();
        services.AddScoped<IMaintenanceDetailRepository, MaintenanceDetailRepository>();
        services.AddScoped<IProcurementDetailRepository, ProcurementDetailRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ITicketWorkflowLogRepository, TicketWorkflowLogRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IWorkflowStepRepository, WorkflowStepRepository>();
        #endregion

        return services;
    }
}
