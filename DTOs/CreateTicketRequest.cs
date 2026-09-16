using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record CreateTicketRequest(
    long EquipmentId,
    long CreatedById,
    string Title,
    string? Description,
    TicketPriority Priority,
    DateOnly DueDate);
