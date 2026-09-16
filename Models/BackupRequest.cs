using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class BackupRequest
{
    [MaxLength(50)]
    public string BedNumber { get; set; } = string.Empty;
    public long Id { get; set; }

    [MaxLength(50)]
    public string RequestNumber { get; set; } = string.Empty;
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

    [MaxLength(500)]
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
