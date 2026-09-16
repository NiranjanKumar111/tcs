using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public record ReferenceData(IReadOnlyList<EquipmentType> EquipmentTypes, IReadOnlyList<Location> Locations);
