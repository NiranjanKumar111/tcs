using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class Notification
{
    public long Id { get; set; }
    public long RecipientUserId { get; set; }
    public long? TriggeredByUserId { get; set; }
    public NotificationType Type { get; set; } = NotificationType.info;

    [MaxLength(250)]
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
