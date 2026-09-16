using EquipmentManagementBackend.Models;

namespace EquipmentManagementBackend.DTOs;

public record EquipmentAssignees(IReadOnlyList<User> Technicians, IReadOnlyList<User> Approvers);
