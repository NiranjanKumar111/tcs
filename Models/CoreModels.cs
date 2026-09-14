using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class User
{
    public long Id { get; set; }
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string Email { get; set; } = string.Empty;
    [MaxLength(50)] public string Role { get; set; } = "user";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Location
{
    public bool IsCentralWarehouse { get; set; }
    public long Id { get; set; }
    [MaxLength(100)] public string Code { get; set; } = string.Empty;
    [MaxLength(200)] public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class EquipmentType
{
    public long Id { get; set; }
    [MaxLength(80)] public string TypeCode { get; set; } = string.Empty;
    [MaxLength(150)] public string TypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
}

public class Equipment
{
    public long Id { get; set; }
    [MaxLength(80)] public string EquipmentCode { get; set; } = string.Empty;
    [MaxLength(200)] public string Name { get; set; } = string.Empty;
    public long EquipmentTypeId { get; set; }
    public long LocationId { get; set; }
    [MaxLength(120)] public string? SerialNumber { get; set; }
    [MaxLength(120)] public string? Manufacturer { get; set; }
    [MaxLength(120)] public string? ModelNumber { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public EquipmentType? EquipmentType { get; set; }
    public Location? Location { get; set; }
}

public class EquipmentTechnician
{
    public long EquipmentId { get; set; }
    public long TechnicianId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EquipmentApprover
{
    public long EquipmentId { get; set; }
    public long ApproverId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EquipmentCalibrationRule
{
    public long RuleId { get; set; }
    [MaxLength(1000)] public string RuleText { get; set; } = string.Empty;
    public long EquipmentId { get; set; }
}

public class Ticket
{
    public long TicketId { get; set; }
    [MaxLength(50)] public string TicketNumber { get; set; } = string.Empty;
    public long EquipmentId { get; set; }
    public long CreatedById { get; set; }
    public long? AssignedTo { get; set; }
    public long? ApproverId { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.open;
    public TicketPriority Priority { get; set; } = TicketPriority.medium;
    [MaxLength(250)] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly DueDate { get; set; }
    public int VersionNumber { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Equipment? Equipment { get; set; }
}

public class TicketHistory
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public TicketHistoryAction Action { get; set; }
    public TicketStatus? NewStatus { get; set; }
    public long? NewAssignedTo { get; set; }
    public string? Remarks { get; set; }
    public long PerformedBy { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
}

public class TicketDocument
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public TicketDocumentType DocumentType { get; set; }
    [MaxLength(255)] public string OriginalFileName { get; set; } = string.Empty;
    [MaxLength(255)] public string StoredFileName { get; set; } = string.Empty;
    [MaxLength(1000)] public string StoragePath { get; set; } = string.Empty;
    [MaxLength(100)] public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public long UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public class MaintenanceSchedule
{
    public long Id { get; set; }
    public long EquipmentId { get; set; }
    public long? TicketId { get; set; }
    public DateOnly DueDate { get; set; }
    [MaxLength(50)] public string ScheduleType { get; set; } = "preventive_maintenance";
    public bool IsCompleted { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class BackupRequest
{
    [MaxLength(50)] public string BedNumber { get; set; } = string.Empty;
    public long Id { get; set; }
    [MaxLength(50)] public string RequestNumber { get; set; } = string.Empty;
    public WardType RequestedWard { get; set; }
    public long RequestedBy { get; set; }
    public long EquipmentTypeId { get; set; }
    public long? RequestedEquipmentId { get; set; }
    public int Quantity { get; set; } = 1;
    public BackupRequestPriority Priority { get; set; } = BackupRequestPriority.medium;
    public string Reason { get; set; } = string.Empty;
    public BackupRequestStatus Status { get; set; } = BackupRequestStatus.requested;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public long? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    [MaxLength(500)] public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class BackupAllocation
{
    public BackupAllocationStatus Status { get; set; } = BackupAllocationStatus.reserved;
    public DateTime ReservationExpiresAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public long Id { get; set; }
    public long RequestId { get; set; }
    public long EquipmentId { get; set; }
    public long AllocatedBy { get; set; }
    public long? PickedUpBy { get; set; }
    public long? DeliveredBy { get; set; }
    public long? ReceivedBy { get; set; }
    public long? ReturnedBy { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
}

public class BackupEscalation
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public long? AssignedAdminId { get; set; }
    public long? ResolvedBy { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

public class Notification
{
    public long Id { get; set; }
    public long RecipientUserId { get; set; }
    public long? TriggeredByUserId { get; set; }
    public NotificationType Type { get; set; } = NotificationType.info;
    [MaxLength(250)] public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public AuditAction Action { get; set; }
    [MaxLength(50)] public string? EntityType { get; set; }
    [MaxLength(50)] public string? EntityId { get; set; }
    [MaxLength(500)] public string Description { get; set; } = string.Empty;
    public AuditResult Result { get; set; } = AuditResult.success;
    [MaxLength(45)] public string? IpAddress { get; set; }
    [MaxLength(100)] public string? RequestId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime RetentionUntil { get; set; } = DateTime.UtcNow.AddYears(7);
    public DateTime? ArchivedAt { get; set; }
}
