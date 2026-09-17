namespace EquipmentManagementBackend.DTOs;

public record ExceptionRecord(
    string TicketId,
    string Type,
    string EquipmentName,
    string Status,
    string Priority);
