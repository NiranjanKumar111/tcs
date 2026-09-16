using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record SaveEquipmentInput(
    string Name,
    long EquipmentTypeId,
    long LocationId,
    MaintenanceFrequency Frequency,
    long TechnicianId,
    long ApproverId,
    string? SerialNumber = null,
    string? Manufacturer = null,
    string? ModelNumber = null);
