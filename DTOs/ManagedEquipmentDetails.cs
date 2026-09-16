using EquipmentManagementBackend.Models;

namespace EquipmentManagementBackend.DTOs;

public record ManagedEquipmentDetails(EquipmentView Inventory,
    IReadOnlyList<User> Technicians, IReadOnlyList<User> Approvers);
