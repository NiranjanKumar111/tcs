using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class EquipmentApprover
{
    public long EquipmentId { get; set; }
    public long ApproverId { get; set; }
    public bool IsActive { get; set; } = true;
}
