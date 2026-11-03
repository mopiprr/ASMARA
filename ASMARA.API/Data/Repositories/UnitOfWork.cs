using Asmara.API.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data.Repositories;

public class UnitOfWork(AsmaraDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
       => context.SaveChangesAsync(ct);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        var result = await action(ct); 
        await tx.CommitAsync(ct);
        return result;
    }
}
