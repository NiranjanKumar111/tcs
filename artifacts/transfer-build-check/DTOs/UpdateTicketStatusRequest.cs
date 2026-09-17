using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record UpdateTicketStatusRequest(
    TicketStatus Status,
    long PerformedBy,
    string? Remarks,
    int VersionNumber);
