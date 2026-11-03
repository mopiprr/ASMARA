using ASMARA.API.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data.Repositories;

public class UnitOfWork(AsmaraDbContext context) : IUnitOfWork
{
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
