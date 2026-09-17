using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

internal sealed class EfRepository<T> : IRepository<T>, IAsyncEnumerable<T> where T : class
{
    private readonly DbSet<T> entities;

    public EfRepository(DbSet<T> entities)
    {
        this.entities = entities;
    }

    private IQueryable<T> Query => entities;

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => entities.AsAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
    public Type ElementType => Query.ElementType;
    public Expression Expression => Query.Expression;
    public IQueryProvider Provider => Query.Provider;

    public IEnumerator<T> GetEnumerator() => Query.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Add(T entity) => entities.Add(entity);
    public void Remove(T entity) => entities.Remove(entity);
    public ValueTask<T?> FindAsync(params object? [] keys) => entities.FindAsync(keys);
}
