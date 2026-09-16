using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.DTOs;

namespace EquipmentManagementBackend.Application;

public interface ILocationService
{
    Task<long> SaveLocationAsync(
        Session actor,
        long? id,
        string code,
        string name,
        string building,
        string floor,
        WardType ward,
        string? room,
        string? shelf,
        bool warehouse);
}
