using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

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
