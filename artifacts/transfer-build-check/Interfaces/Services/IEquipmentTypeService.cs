using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.DTOs;

namespace EquipmentManagementBackend.Application;

public interface IEquipmentTypeService
{
    Task<long> AddTypeAsync(Session actor, string code, string name);
}
