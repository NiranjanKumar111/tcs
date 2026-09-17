using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class Location
{
    [MaxLength(150)]
    public string Building { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Floor { get; set; } = string.Empty;
    public WardType Ward { get; set; } = WardType.OTHER;

    [MaxLength(150)]
    public string? Room { get; set; }

    [MaxLength(150)]
    public string? Shelf { get; set; }
    public bool IsCentralWarehouse { get; set; }
    public long Id { get; set; }

    [MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
