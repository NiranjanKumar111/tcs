using EquipmentManagementBackend.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace EquipmentManagementBackend.Application;

public interface IApplicationRepository
{
    IUnitOfWork Open();
}
