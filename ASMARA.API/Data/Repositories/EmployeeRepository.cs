using ASMARA.API.Common.Security;
using ASMARA.API.Entities;
using ASMARA.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data.Repositories;

public class EmployeeRepository(AsmaraDbContext context) : Repository<Employee>(context), IEmployeeRepository
{

    public async Task<Guid?> GetManagerIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var managerId = await Set
            .Where(e => e.Id == employeeId)
            .Select(e => e.ManagerId)
            .FirstOrDefaultAsync(ct);

        if (managerId == null)
        {
            return null;
        }

        return managerId;
    }

    public IQueryable<Employee> GetActiveHandlers()
        => Set.Where(e => e.EmployeeStatus == (byte)EmployeeStatusEnum.Available
                       && e.Account != null
                       && e.Account.IsActive
                       && e.Account.Roles.Any(r => r.RoleCode == Roles.MaintenanceHandler && r.IsActive));
}