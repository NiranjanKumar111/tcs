using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class TicketComplianceItem
{
    public long Id { get; set; }
    public long TicketId { get; set; }

    [MaxLength(250)]
    public string Description { get; set; } = string.Empty;
    public bool Passed { get; set; }

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;
}
