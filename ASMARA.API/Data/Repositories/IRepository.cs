namespace ASMARA.API.Data.Repositories;

public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default);
    IQueryable<TEntity> Query();  
    IQueryable<TEntity> QueryNoTracking(); 
    void Add(TEntity entity);
    void AddRange(IEnumerable<TEntity> entities);
    void Remove(TEntity entity);
}
