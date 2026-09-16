using System.ComponentModel.DataAnnotations;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public class EquipmentQuery : PageQuery
{
    [MaxLength(200)]
    public string? Search { get; set; }
    public long? EquipmentTypeId { get; set; }
    public long? LocationId { get; set; }
    public bool? IsCentralWarehouse { get; set; }
    public bool? IsActive { get; set; } = true;
    public EquipmentAvailability? Availability { get; set; }
}
