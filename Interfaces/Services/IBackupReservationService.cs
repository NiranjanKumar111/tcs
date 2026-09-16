using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;

namespace EquipmentManagementBackend.Application;

public interface IBackupReservationService
{
    Task<BackupDetails> CreateAsync(CreateBackupRequestRequest input, CancellationToken ct);
    Task<BackupDetails> ReserveAsync(long id, long actor, CancellationToken ct);
    Task<BackupDetails> PickupAsync(
        long requestId,
        long allocationId,
        long actor,
        CancellationToken ct);
    Task<BackupDetails> ReturnAsync(
        long requestId,
        long allocationId,
        long actor,
        CancellationToken ct);
    Task<BackupDetails> CancelAsync(long id, CancelBackupRequest input, CancellationToken ct);
    Task<int> ExpireAsync(CancellationToken ct);
    Task<int> ExpireWithinTransactionAsync(CancellationToken ct);
    Task<BackupDetails> GetAsync(long id, CancellationToken ct);
    Task<PagedResult<BackupRequest>> ListAsync(BackupQuery query, CancellationToken ct);
    Task<PagedResult<BackupEscalation>> EscalationsAsync(PageQuery query, bool unresolvedOnly, CancellationToken ct);
    Task ValidateUserAsync(long id, CancellationToken ct);
}
