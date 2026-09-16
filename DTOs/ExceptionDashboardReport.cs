namespace EquipmentManagementBackend.DTOs;

public record ExceptionDashboardReport(
    DateTime GeneratedAt,
    IReadOnlyList<ExceptionRecord> OverdueMaintenance,
    IReadOnlyList<ExceptionRecord> UnresolvedTickets,
    IReadOnlyList<ExceptionRecord> BackupExceptions);
