using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record CreateEquipmentRequest(
    string EquipmentCode,
    string Name,
    long EquipmentTypeId,
    long LocationId,
    string? SerialNumber,
    string? Manufacturer,
    string? ModelNumber,
    DateOnly? PurchaseDate,
    long? CreatedBy);
