using EquipmentManagementBackend.DTOs;

namespace EquipmentManagementBackend.Application;

public interface IExceptionDashboardService
{
    Task<ExceptionDashboardReport> GetAsync(Session actor);
}
