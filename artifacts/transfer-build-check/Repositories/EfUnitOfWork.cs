using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EquipmentManagementBackend.Application;

internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext context;

    public EfUnitOfWork(AppDbContext context)
    {
        this.context = context;
    }

    public long? ActorId { get; set; }
    public IRepository<TicketComplianceVerification> TicketComplianceVerifications => new EfRepository<TicketComplianceVerification>(context.TicketComplianceVerifications);
    public IRepository<TicketComplianceItem> TicketComplianceItems => new EfRepository<TicketComplianceItem>(context.TicketComplianceItems);
    public IRepository<TicketCalibrationResult> TicketCalibrationResults => new EfRepository<TicketCalibrationResult>(context.TicketCalibrationResults);
    public IRepository<User> Users => new EfRepository<User>(context.Users);
    public IRepository<Location> Locations => new EfRepository<Location>(context.Locations);
    public IRepository<EquipmentType> EquipmentTypes => new EfRepository<EquipmentType>(context.EquipmentTypes);
    public IRepository<Equipment> Equipment => new EfRepository<Equipment>(context.Equipment);
    public IRepository<EquipmentTechnician> EquipmentTechnicians => new EfRepository<EquipmentTechnician>(context.EquipmentTechnicians);
    public IRepository<EquipmentApprover> EquipmentApprovers => new EfRepository<EquipmentApprover>(context.EquipmentApprovers);
    public IRepository<EquipmentCalibrationRule> EquipmentCalibrationRules => new EfRepository<EquipmentCalibrationRule>(context.EquipmentCalibrationRules);
    public IRepository<Ticket> Tickets => new EfRepository<Ticket>(context.Tickets);
    public IRepository<TicketHistory> TicketHistory => new EfRepository<TicketHistory>(context.TicketHistory);
    public IRepository<TicketDocument> TicketDocuments => new EfRepository<TicketDocument>(context.TicketDocuments);
    public IRepository<MaintenanceSchedule> MaintenanceSchedules => new EfRepository<MaintenanceSchedule>(context.MaintenanceSchedules);
    public IRepository<BackupRequest> BackupRequests => new EfRepository<BackupRequest>(context.BackupRequests);
    public IRepository<BackupAllocation> BackupAllocations => new EfRepository<BackupAllocation>(context.BackupAllocations);
    public IRepository<BackupEscalation> BackupEscalations => new EfRepository<BackupEscalation>(context.BackupEscalations);
    public IRepository<Notification> Notifications => new EfRepository<Notification>(context.Notifications);
    public IRepository<AuditLog> AuditLogs => new EfRepository<AuditLog>(context.AuditLogs);

    public Task<IDbContextTransaction> BeginInventoryAsync(CancellationToken cancellation = default) => InventoryTransaction.BeginAsync(context, cancellation);
    public async Task<int> SaveChangesAsync(CancellationToken cancellation = default)
    {
        context.ChangeTracker.DetectChanges();
        var changes = context.ChangeTracker.Entries().Where(e => e.Entity is not AuditLog && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).Select(e => (Entry: e, State: e.State, Fields: string.Join(",", e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name)))).ToList();
        await using var ownTransaction = context.Database.CurrentTransaction == null ? await context.Database.BeginTransactionAsync(cancellation) : null;
        var count = await context.SaveChangesAsync(cancellation);
        foreach (var change in changes)
        {
            var key = change.Entry.Metadata.FindPrimaryKey();
            var id = key == null ? "" : string.Join(",", key.Properties.Select(p => change.Entry.Property(p.Name).CurrentValue));
            var description = $"{change.State}: {change.Entry.Metadata.ClrType.Name}; fields: {change.Fields}";
            context.AuditLogs.Add(new AuditLog {
                    UserId = ActorId,
                    EntityType = change.Entry.Metadata.ClrType.Name,
                    EntityId = id,
                    Action = change.State == EntityState.Added ? AuditAction.create : change.State == EntityState.Deleted ? AuditAction.delete : AuditAction.update,
                    Description = description.Length > 500 ? description[..500] : description
                });
        }

        if (changes.Count > 0)
        {
            await context.SaveChangesAsync(cancellation);
        }

        if (ownTransaction != null)
        {
            await ownTransaction.CommitAsync(cancellation);
        }

        return count;
    }

    public ValueTask DisposeAsync() => context.DisposeAsync();
}
