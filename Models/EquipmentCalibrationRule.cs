using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class EquipmentCalibrationRule
{
    public long RuleId { get; set; }

    [MaxLength(1000)]
    public string RuleText { get; set; } = string.Empty;
    public long EquipmentId { get; set; }
}
