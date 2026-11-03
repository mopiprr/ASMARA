using ASMARA.API.Data;
using ASMARA.API.Entities;
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
}