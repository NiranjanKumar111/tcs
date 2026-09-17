using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class EquipmentType
{
    public long Id { get; set; }

    [MaxLength(80)]
    public string TypeCode { get; set; } = string.Empty;

    [MaxLength(150)]
    public string TypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
}
