using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class Database : IApplicationRepository
{
    private readonly DbContextOptions<AppDbContext> options;

    public Database(DbContextOptions<AppDbContext> options)
    {
        this.options = options;
    }

    public AppDbContext Open() => new(options);
    IUnitOfWork IApplicationRepository.Open() => new EfUnitOfWork(Open());
    public static void Audit(
        IUnitOfWork db,
        long actor,
        string action,
        string entity,
        long id,
        EquipmentManagementBackend.Models.Enums.AuditAction auditAction = EquipmentManagementBackend.Models.Enums.AuditAction.update)
    {
        db.AuditLogs.Add(new AuditLog {
                UserId = actor,
                Action = auditAction,
                EntityType = entity,
                EntityId = id.ToString(),
                Description = action
            });
    }
}
