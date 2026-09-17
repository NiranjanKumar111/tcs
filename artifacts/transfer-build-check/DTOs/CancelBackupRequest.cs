using System.ComponentModel.DataAnnotations;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record CancelBackupRequest([Range(1, long.MaxValue)] long PerformedBy, [Required, MaxLength(500)] string Reason);
