using System.ComponentModel.DataAnnotations;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public class BackupQuery : PageQuery
{
    public long? RequestedBy { get; set; }
    public WardType? RequestedWard { get; set; }
    public long? EquipmentTypeId { get; set; }
    public BackupRequestStatus? Status { get; set; }

    [MaxLength(50)]
    public string? BedNumber { get; set; }
}
