using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record TicketDetails(
    Ticket Ticket,
    IReadOnlyList<TicketHistory> History,
    IReadOnlyList<TicketComplianceItem> Checklist,
    IReadOnlyList<TicketCalibrationResult> Readings,
    IReadOnlyList<TicketComplianceVerification> Reviews,
    string? TechnicianName = null,
    string? ApproverName = null);
