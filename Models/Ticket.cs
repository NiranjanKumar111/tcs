using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class Ticket
{
    public TicketType TicketType { get; set; } = TicketType.corrective;
    public string? WorkReport { get; set; }
    public DateOnly? WorkCompletedOn { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public long TicketId { get; set; }

    [MaxLength(50)]
    public string TicketNumber { get; set; } = string.Empty;
    public long EquipmentId { get; set; }
    public long CreatedById { get; set; }
    public long? AssignedTo { get; set; }
    public long? ApproverId { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.open;
    public TicketPriority Priority { get; set; } = TicketPriority.medium;

    [MaxLength(250)]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly DueDate { get; set; }
    public int VersionNumber { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Equipment? Equipment { get; set; }
}
