using ASMARA.API.Data;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Data.Repositories;

public class Repository<TEntity>(AsmaraDbContext context) : IRepository<TEntity> where TEntity : class
{
    protected readonly AsmaraDbContext Context = context;
    protected readonly DbSet<TEntity> Set = context.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default)
        => await Set.FindAsync([id], ct);

    public IQueryable<TEntity> Query() => Set;
    public IQueryable<TEntity> QueryNoTracking() => Set.AsNoTracking();
    public void Add(TEntity entity) => Set.Add(entity);
    public void AddRange(IEnumerable<TEntity> entities) => Set.AddRange(entities);
    public void Remove(TEntity entity) => Set.Remove(entity);
}
