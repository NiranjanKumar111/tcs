using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.DTOs;

namespace EquipmentManagementBackend.Application;

public interface IEmployeeService
{
    Task<long> SaveEmployeeAsync(
        Session actor,
        long? id,
        string name,
        string email,
        string role,
        bool active,
        string? password);
    Task DeleteEmployeeAsync(Session actor, long id);
    Task<List<User>> EmployeesAsync(Session actor);
    Task<User> GetEmployeeAsync(Session actor, long id);
}
