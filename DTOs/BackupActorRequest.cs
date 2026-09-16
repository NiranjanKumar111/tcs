using System.ComponentModel.DataAnnotations;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record BackupActorRequest([Range(1, long.MaxValue)] long PerformedBy);
