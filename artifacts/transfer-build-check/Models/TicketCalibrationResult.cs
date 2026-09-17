using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class TicketCalibrationResult
{
    public long Id { get; set; }
    public long TicketId { get; set; }

    [MaxLength(150)]
    public string Parameter { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Unit { get; set; } = string.Empty;
    public decimal Minimum { get; set; }
    public decimal Maximum { get; set; }
    public decimal Measured { get; set; }
    public bool Passed { get; set; }
}
