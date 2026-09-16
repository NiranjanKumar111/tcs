using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.DTOs;

namespace EquipmentManagementBackend.Application;

public interface IBackupsService
{
    Task<BackupDetails> RequestAsync(
        Session actor,
        long type,
        WardType ward,
        string bed,
        long? preferred);
    Task<BackupDetails> DetailsAsync(Session actor, long requestId);
    Task<PagedResult<BackupRequest>> ListAsync(Session actor, int page = 1, bool ownOnly = false);
    Task<BackupDetails> ActionAsync(
        Session actor,
        string action,
        long id,
        long allocationId = 0);
    Task RunExpiryAsync(CancellationToken cancellation);
}
