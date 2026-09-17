using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class TicketComplianceVerification
{
    public long Id { get; set; }
    public long TicketId { get; set; }
    public long ApproverId { get; set; }
    public bool Approved { get; set; }
    public int TicketVersion { get; set; }

    [MaxLength(2000)]
    public string Notes { get; set; } = string.Empty;
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}
