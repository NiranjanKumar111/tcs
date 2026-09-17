using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

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
