using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record UpdateEquipmentRequest(
    string Name,
    long EquipmentTypeId,
    long LocationId,
    string? SerialNumber,
    string? Manufacturer,
    string? ModelNumber,
    DateOnly? PurchaseDate,
    bool IsActive,
    long? UpdatedBy);
