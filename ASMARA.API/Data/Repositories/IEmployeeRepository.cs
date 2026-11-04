using ASMARA.API.Entities;

namespace ASMARA.API.Data.Repositories;

public interface IEmployeeRepository : IRepository<Employee>
{
    Task<Guid?> GetManagerIdAsync(Guid employeeId, CancellationToken ct = default);
}
