using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record DashboardReport(
    DateTime GeneratedAt,
    int ActiveEquipmentCount,
    int OpenTicketCount,
    int PendingApprovalCount,
    int OverdueTicketCount,
    int OpenEscalationCount,
    int ConfiguredMaintenanceCount,
    int VerifiedMaintenanceCount,
    int ConfiguredCalibrationCount,
    int VerifiedCalibrationCount,
    int OverdueMaintenanceCount,
    int OverdueCalibrationCount,
    bool IncludesExceptions,
    IReadOnlyList<Equipment> EquipmentExceptions,
    IReadOnlyList<Ticket> TicketExceptions,
    IReadOnlyList<BackupEscalation> Escalations);
