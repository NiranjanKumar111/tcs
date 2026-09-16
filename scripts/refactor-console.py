from pathlib import Path
import re
root=Path('ConsoleApp')
def write(p,s):
 p=root/p;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(s,encoding='utf-8')
models=re.findall(r'public DbSet<(\w+)> (\w+)',Path('Data/AppDbContext.cs').read_text())
write(Path('Interfaces/Repositories/IApplicationRepository.cs'),'''using EquipmentManagementBackend.Models;
using Microsoft.EntityFrameworkCore.Storage;
namespace CriticalCare.ConsoleApp;
public interface IRepository<T> : IQueryable<T> where T : class
{
    void Add(T entity);
    void Remove(T entity);
    ValueTask<T?> FindAsync(params object?[] keys);
}
public interface IApplicationRepository { IUnitOfWork Open(); }
public interface IUnitOfWork : IAsyncDisposable
{
    long? ActorId { get; set; }
    Task<int> SaveChangesAsync(CancellationToken cancellation = default);
    Task<IDbContextTransaction> BeginInventoryAsync(CancellationToken cancellation = default);
'''+''.join(f'    IRepository<{t}> {n} {{ get; }}\n' for t,n in models)+'}\n')
write(Path('Repositories/EfRepository.cs'),'''using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
namespace CriticalCare.ConsoleApp;
internal sealed class EfRepository<T>(DbSet<T> entities) : IRepository<T> where T : class
{
    private IQueryable<T> Query => entities;
    public Type ElementType => Query.ElementType;
    public Expression Expression => Query.Expression;
    public IQueryProvider Provider => Query.Provider;
    public IEnumerator<T> GetEnumerator() => Query.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Add(T entity) => entities.Add(entity);
    public void Remove(T entity) => entities.Remove(entity);
    public ValueTask<T?> FindAsync(params object?[] keys) => entities.FindAsync(keys);
}
''')
write(Path('Repositories/EfUnitOfWork.cs'),'''using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace CriticalCare.ConsoleApp;
internal sealed class EfUnitOfWork(AppDbContext context) : IUnitOfWork
{
    public long? ActorId { get; set; }
'''+''.join(f'    public IRepository<{t}> {n} => new EfRepository<{t}>(context.{n});\n' for t,n in models)+'''
    public Task<IDbContextTransaction> BeginInventoryAsync(CancellationToken cancellation = default) => InventoryTransaction.BeginAsync(context, cancellation);
    public async Task<int> SaveChangesAsync(CancellationToken cancellation = default)
    {
        context.ChangeTracker.DetectChanges();
        var changes = context.ChangeTracker.Entries().Where(e => e.Entity is not AuditLog &&
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (Entry: e, State: e.State, Fields: string.Join(",", e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name)))).ToList();
        await using var ownTransaction = context.Database.CurrentTransaction == null ? await context.Database.BeginTransactionAsync(cancellation) : null;
        var count = await context.SaveChangesAsync(cancellation);
        foreach (var change in changes)
        {
            var key = change.Entry.Metadata.FindPrimaryKey();
            var id = key == null ? "" : string.Join(",", key.Properties.Select(p => change.Entry.Property(p.Name).CurrentValue));
            var description = $"{change.State}: {change.Entry.Metadata.ClrType.Name}; fields: {change.Fields}";
            context.AuditLogs.Add(new AuditLog {
                UserId = ActorId, EntityType = change.Entry.Metadata.ClrType.Name, EntityId = id,
                Action = change.State == EntityState.Added ? AuditAction.create : change.State == EntityState.Deleted ? AuditAction.delete : AuditAction.update,
                Description = description.Length > 500 ? description[..500] : description
            });
        }
        if (changes.Count > 0) await context.SaveChangesAsync(cancellation);
        if (ownTransaction != null) await ownTransaction.CommitAsync(cancellation);
        return count;
    }
    public ValueTask DisposeAsync() => context.DisposeAsync();
}
''')
p=root/'Database.cs';s=p.read_text().replace('public sealed class Database(DbContextOptions<AppDbContext> options)','public sealed class Database(DbContextOptions<AppDbContext> options) : IApplicationRepository').replace('    public static void Audit(AppDbContext db,','    IUnitOfWork IApplicationRepository.Open() => new EfUnitOfWork(Open());\n    public static void Audit(IUnitOfWork db,');write(Path('Repositories/Database.cs'),s);p.unlink()
for name in ['Authentication','Administration','Maintenance','Reports','Backups']:
 p=root/(name+'.cs');s=p.read_text().replace('Database database','IApplicationRepository database').replace('Authentication auth','IAuthenticationService auth').replace('EquipmentManagementBackend.Data.AppDbContext db','IUnitOfWork db').replace('AppDbContext db','IUnitOfWork db');s=re.sub(r'InventoryTransaction.BeginAsync\(db, (\w+)\)',r'db.BeginInventoryAsync(\1)',s)
 s=s.replace(f'public sealed class {name}(',f'public sealed class {name}(')
 s=s.replace('auth)\n{',f'auth) : I{name}Service\n{{') if name!='Authentication' else s.replace('database)\n{','database) : IAuthenticationService\n{')
 if name=='Authentication': s=s.replace('        return user;','        db.ActorId = user.Id;\n        return user;')
 # Extract public service contracts before moving.
 methods=re.findall(r'    public (?:async )?(Task[^\n]*?\([^;{]*?\))\s*\n    \{',s,re.S)
 methods=[m for m in methods if '\n    public' not in m]
 write(Path('Interfaces/Services/I'+name+'Service.cs'),'using EquipmentManagementBackend.Models;\nusing EquipmentManagementBackend.Models.Enums;\nusing EquipmentManagementBackend.DTOs;\nnamespace CriticalCare.ConsoleApp;\npublic interface I'+name+'Service\n{\n'+''.join('    '+m+';\n' for m in methods)+'}\n')
 write(Path('Services')/name/(name+'.cs'),s);p.unlink()
for name in ['BackupReservationService','EquipmentService']:
 s=Path('Services',name+'.cs').read_text().replace('namespace EquipmentManagementBackend.Services;','namespace CriticalCare.ConsoleApp;').replace('AppDbContext db','IUnitOfWork db');s='using EquipmentManagementBackend.Services;\n'+s;s=re.sub(r'InventoryTransaction.BeginAsync\(db, (\w+)\)',r'db.BeginInventoryAsync(\1)',s)
 write(Path('Services/Inventory')/(name+'.cs'),s)
p=root/'CriticalCare.Console.csproj';s=p.read_text();s=re.sub(r'    <Compile Include="../Services/(BackupReservationService|EquipmentService).cs"[^\n]*\n','',s);p.write_text(s)
for name,dest in [('Schedule','Services/Maintenance'),('DesignTimeFactory','Infrastructure'),('ServiceException','Infrastructure'),('SelfTests','Tests')]:
 p=root/(name+'.cs');write(Path(dest)/(name+'.cs'),p.read_text());p.unlink()
