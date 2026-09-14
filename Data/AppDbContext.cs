using EquipmentManagementBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<EquipmentTechnician> EquipmentTechnicians => Set<EquipmentTechnician>();
    public DbSet<EquipmentApprover> EquipmentApprovers => Set<EquipmentApprover>();
    public DbSet<EquipmentCalibrationRule> EquipmentCalibrationRules => Set<EquipmentCalibrationRule>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketHistory> TicketHistory => Set<TicketHistory>();
    public DbSet<TicketDocument> TicketDocuments => Set<TicketDocument>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<BackupRequest> BackupRequests => Set<BackupRequest>();
    public DbSet<BackupAllocation> BackupAllocations => Set<BackupAllocation>();
    public DbSet<BackupEscalation> BackupEscalations => Set<BackupEscalation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Table names
        b.Entity<User>().ToTable("users");
        b.Entity<Location>().ToTable("locations");
        b.Entity<EquipmentType>().ToTable("equipment_types");
        b.Entity<Equipment>().ToTable("equipment");
        b.Entity<EquipmentTechnician>().ToTable("equipment_technicians");
        b.Entity<EquipmentApprover>().ToTable("equipment_approvers");
        b.Entity<EquipmentCalibrationRule>().ToTable("equipment_calibration_rule");
        b.Entity<Ticket>().ToTable("tickets");
        b.Entity<TicketHistory>().ToTable("ticket_history");
        b.Entity<TicketDocument>().ToTable("ticket_documents");
        b.Entity<MaintenanceSchedule>().ToTable("maintenance_schedules");
        b.Entity<BackupRequest>().ToTable("backup_requests");
        b.Entity<BackupAllocation>().ToTable("backup_allocations");
        b.Entity<BackupEscalation>().ToTable("backup_escalations");
        b.Entity<Notification>().ToTable("notifications");
        b.Entity<AuditLog>().ToTable("audit_logs");

        // Keys / uniqueness
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Location>().HasIndex(x => x.Code).IsUnique();
        b.Entity<EquipmentType>().HasIndex(x => x.TypeCode).IsUnique();
        b.Entity<Equipment>().HasIndex(x => x.EquipmentCode).IsUnique();
        b.Entity<Ticket>().HasKey(x => x.TicketId);
        b.Entity<Ticket>().HasIndex(x => x.TicketNumber).IsUnique();
        b.Entity<EquipmentCalibrationRule>().HasKey(x => x.RuleId);
        b.Entity<EquipmentTechnician>().HasKey(x => new { x.EquipmentId, x.TechnicianId });
        b.Entity<EquipmentApprover>().HasKey(x => new { x.EquipmentId, x.ApproverId });
        b.Entity<BackupRequest>().HasIndex(x => x.RequestNumber).IsUnique();
        // Database backstop: even a writer outside the service cannot double allocate equipment.
        b.Entity<BackupAllocation>().HasIndex(x => x.EquipmentId).IsUnique()
            .HasFilter("\"Status\" IN ('reserved', 'in_use')");
        b.Entity<BackupAllocation>().HasIndex(x => x.RequestId).IsUnique()
            .HasFilter("\"Status\" IN ('reserved', 'in_use')");
        b.Entity<BackupAllocation>().HasIndex(x => new { x.Status, x.ReservationExpiresAt });
        b.Entity<BackupEscalation>().HasIndex(x => x.RequestId).IsUnique()
            .HasFilter("\"ResolvedAt\" IS NULL");

        // Relationships shown in schema screenshots
        b.Entity<Location>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Location>().HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EquipmentType>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EquipmentType>().HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Equipment>().HasOne(x => x.EquipmentType).WithMany().HasForeignKey(x => x.EquipmentTypeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Equipment>().HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Equipment>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Equipment>().HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EquipmentTechnician>().HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<EquipmentTechnician>().HasOne<User>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EquipmentApprover>().HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<EquipmentApprover>().HasOne<User>().WithMany().HasForeignKey(x => x.ApproverId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EquipmentCalibrationRule>().HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Ticket>().HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Ticket>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Ticket>().HasOne<User>().WithMany().HasForeignKey(x => x.AssignedTo).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Ticket>().HasOne<User>().WithMany().HasForeignKey(x => x.ApproverId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TicketHistory>().HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TicketHistory>().HasOne<User>().WithMany().HasForeignKey(x => x.NewAssignedTo).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TicketHistory>().HasOne<User>().WithMany().HasForeignKey(x => x.PerformedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TicketDocument>().HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TicketDocument>().HasOne<User>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);

        b.Entity<MaintenanceSchedule>().HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MaintenanceSchedule>().HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<MaintenanceSchedule>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MaintenanceSchedule>().HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.Restrict);

        b.Entity<BackupRequest>().HasOne<User>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupRequest>().HasOne<EquipmentType>().WithMany().HasForeignKey(x => x.EquipmentTypeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupRequest>().HasOne<Equipment>().WithMany().HasForeignKey(x => x.RequestedEquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupRequest>().HasOne<User>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupAllocation>().HasOne<BackupRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BackupAllocation>().HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupAllocation>().HasOne<User>().WithMany().HasForeignKey(x => x.AllocatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupAllocation>().HasOne<User>().WithMany().HasForeignKey(x => x.PickedUpBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupAllocation>().HasOne<User>().WithMany().HasForeignKey(x => x.DeliveredBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupAllocation>().HasOne<User>().WithMany().HasForeignKey(x => x.ReceivedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupAllocation>().HasOne<User>().WithMany().HasForeignKey(x => x.ReturnedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupEscalation>().HasOne<BackupRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BackupEscalation>().HasOne<User>().WithMany().HasForeignKey(x => x.AssignedAdminId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BackupEscalation>().HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Notification>().HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Notification>().HasOne<User>().WithMany().HasForeignKey(x => x.TriggeredByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AuditLog>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);

        // Store enums as readable strings in PostgreSQL.
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties().Where(p => p.ClrType.IsEnum || Nullable.GetUnderlyingType(p.ClrType)?.IsEnum == true))
            {
                var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (Nullable.GetUnderlyingType(property.ClrType) is not null)
                {
                    var converterType = typeof(Microsoft.EntityFrameworkCore.Storage.ValueConversion.EnumToStringConverter<>).MakeGenericType(enumType);
                    var converter = (Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter)Activator.CreateInstance(converterType)!;
                    property.SetValueConverter(converter);
                }
                else
                {
                    var converterType = typeof(Microsoft.EntityFrameworkCore.Storage.ValueConversion.EnumToStringConverter<>).MakeGenericType(enumType);
                    var converter = (Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter)Activator.CreateInstance(converterType)!;
                    property.SetValueConverter(converter);
                }
            }
        }

        // Optimistic concurrency, as noted in your ticket schema.
        b.Entity<Ticket>().Property(x => x.VersionNumber).IsConcurrencyToken();

        // Useful indexes from the schema intent.
        b.Entity<Ticket>().HasIndex(x => new { x.AssignedTo, x.Status, x.CreatedAt });
        b.Entity<Ticket>().HasIndex(x => new { x.ApproverId, x.Status, x.CreatedAt });
        b.Entity<Ticket>().HasIndex(x => new { x.Status, x.Priority, x.CreatedAt });
        b.Entity<Ticket>().HasIndex(x => x.DueDate);
        b.Entity<BackupRequest>().HasIndex(x => new { x.RequestedWard, x.Status, x.RequestedAt });
        b.Entity<BackupRequest>().HasIndex(x => new { x.RequestedBy, x.Status, x.RequestedAt });
        b.Entity<BackupRequest>().HasIndex(x => new { x.EquipmentTypeId, x.Status, x.RequestedAt });
        b.Entity<AuditLog>().HasIndex(x => new { x.UserId, x.CreatedAt });
        b.Entity<AuditLog>().HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
