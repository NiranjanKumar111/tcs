using System.ComponentModel.DataAnnotations;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record AllocationView(BackupAllocation Allocation, Equipment Equipment, double RemainingSeconds);
