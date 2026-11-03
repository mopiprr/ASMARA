using Asmara.API.Data;

namespace ASMARA.API.Data.Repositories;

public class UnitOfWork(
    AsmaraDbContext context
) : IUnitOfWork
{

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => context.SaveChangesAsync(ct);
}
