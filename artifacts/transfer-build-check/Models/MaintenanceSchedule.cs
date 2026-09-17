using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class MaintenanceSchedule
{
    public long Id { get; set; }
    public long EquipmentId { get; set; }
    public long? TicketId { get; set; }
    public DateOnly DueDate { get; set; }

    [MaxLength(50)]
    public string ScheduleType { get; set; } = "preventive_maintenance";
    public bool IsCompleted { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
