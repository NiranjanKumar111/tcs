using EquipmentManagementBackend.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace EquipmentManagementBackend.Application;

public interface IRepository<T> : IQueryable<T> where T : class
{
    void Add(T entity);
    void Remove(T entity);
    ValueTask<T?> FindAsync(params object? [] keys);
}
