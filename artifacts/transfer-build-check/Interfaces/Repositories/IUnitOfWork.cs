using EquipmentManagementBackend.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace EquipmentManagementBackend.Application;

public interface IUnitOfWork : IAsyncDisposable
{
    long? ActorId { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellation = default);
    Task<IDbContextTransaction> BeginInventoryAsync(CancellationToken cancellation = default);
    IRepository<TicketComplianceVerification> TicketComplianceVerifications { get; }

    IRepository<TicketComplianceItem> TicketComplianceItems { get; }

    IRepository<TicketCalibrationResult> TicketCalibrationResults { get; }

    IRepository<User> Users { get; }

    IRepository<Location> Locations { get; }

    IRepository<EquipmentType> EquipmentTypes { get; }

    IRepository<Equipment> Equipment { get; }

    IRepository<EquipmentTechnician> EquipmentTechnicians { get; }

    IRepository<EquipmentApprover> EquipmentApprovers { get; }

    IRepository<EquipmentCalibrationRule> EquipmentCalibrationRules { get; }

    IRepository<Ticket> Tickets { get; }

    IRepository<TicketHistory> TicketHistory { get; }

    IRepository<TicketDocument> TicketDocuments { get; }

    IRepository<MaintenanceSchedule> MaintenanceSchedules { get; }

    IRepository<BackupRequest> BackupRequests { get; }

    IRepository<BackupAllocation> BackupAllocations { get; }

    IRepository<BackupEscalation> BackupEscalations { get; }

    IRepository<Notification> Notifications { get; }

    IRepository<AuditLog> AuditLogs { get; }
}
