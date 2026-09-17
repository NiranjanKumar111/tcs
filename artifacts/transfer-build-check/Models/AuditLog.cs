using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class AuditLog
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public AuditAction Action { get; set; }

    [MaxLength(50)]
    public string? EntityType { get; set; }

    [MaxLength(50)]
    public string? EntityId { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    public AuditResult Result { get; set; } = AuditResult.success;

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(100)]
    public string? RequestId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime RetentionUntil { get; set; } = DateTime.UtcNow.AddYears(7);
    public DateTime? ArchivedAt { get; set; }
}
