using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record CreateBackupRequestRequest(
    WardType RequestedWard,
    long RequestedBy,
    long EquipmentTypeId,
    string BedNumber,
    long? RequestedEquipmentId = null,
    int Quantity = 1,
    BackupRequestPriority Priority = BackupRequestPriority.medium,
    string Reason = "");
