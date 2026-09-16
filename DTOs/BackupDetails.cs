using System.ComponentModel.DataAnnotations;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record BackupDetails(
    BackupRequest Request,
    AllocationView? Reservation,
    IReadOnlyList<AllocationView> AllocationHistory,
    IReadOnlyList<BackupEscalation> Escalations,
    DateTime ServerTimeUtc);
