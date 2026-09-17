using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;

namespace EquipmentManagementBackend.Application;

public interface IEquipmentService
{
    Task<PagedResult<EquipmentView>> ListAsync(EquipmentQuery query, CancellationToken ct);
    Task<EquipmentView> GetAsync(long id, CancellationToken ct);
    Task<EquipmentView> CreateAsync(CreateEquipmentRequest request, CancellationToken ct);
    Task<EquipmentView> UpdateAsync(long id, UpdateEquipmentRequest request, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);
    Task AssignAsync(
        long id,
        long userId,
        bool technician,
        CancellationToken ct);
}
