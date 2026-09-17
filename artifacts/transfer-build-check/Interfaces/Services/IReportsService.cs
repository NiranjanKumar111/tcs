using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.DTOs;

namespace EquipmentManagementBackend.Application;

public interface IReportsService
{
    Task<PagedResult<EquipmentView>> EquipmentAsync(Session actor, EquipmentQuery query);
    Task<EquipmentView> TechnicianEquipmentAsync(Session actor, long id);
    Task<ReferenceData> ReferencesAsync(Session actor);
    Task<DashboardReport> DashboardAsync(Session actor, bool exceptions = false);
    Task<IReadOnlyList<AuditLog>> AuditAsync(Session actor);
}
