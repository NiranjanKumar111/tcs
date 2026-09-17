using EquipmentManagementBackend.DTOs;

namespace EquipmentManagementBackend.Application;

public interface IEquipmentManagementService
{
    Task<long> SaveEquipmentAsync(Session actor, long? id, SaveEquipmentInput input);
    Task<EquipmentAssignees> GetAssigneesAsync(Session actor);
    Task<ManagedEquipmentDetails> GetEquipmentAsync(Session actor, string idOrCode);
    Task DeleteEquipmentAsync(Session actor, long id);
}
