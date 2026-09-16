using EquipmentManagementBackend.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EquipmentManagementBackend.Models;

public class Equipment
{
    public MaintenanceFrequency MaintenanceFrequency { get; set; }
    public DateOnly? MaintenanceAnchorDate { get; set; }
    public DateOnly? LastMaintenanceDate { get; set; }
    public DateOnly? NextMaintenanceDate { get; set; }
    public MaintenanceFrequency CalibrationFrequency { get; set; }
    public DateOnly? CalibrationAnchorDate { get; set; }
    public DateOnly? LastCalibrationDate { get; set; }
    public DateOnly? NextCalibrationDate { get; set; }
    public long Id { get; set; }

    [MaxLength(80)]
    public string EquipmentCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    public long EquipmentTypeId { get; set; }
    public long LocationId { get; set; }

    [MaxLength(120)]
    public string? SerialNumber { get; set; }

    [MaxLength(120)]
    public string? Manufacturer { get; set; }

    [MaxLength(120)]
    public string? ModelNumber { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public EquipmentType? EquipmentType { get; set; }
    public Location? Location { get; set; }
}
