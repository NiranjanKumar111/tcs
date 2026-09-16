using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class EquipmentTechnician
{
    public long EquipmentId { get; set; }
    public long TechnicianId { get; set; }
    public bool IsActive { get; set; } = true;
}
