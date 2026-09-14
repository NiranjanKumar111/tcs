using System.ComponentModel.DataAnnotations;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.DTOs;

public class PageQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public class EquipmentQuery : PageQuery
{
    [MaxLength(200)] public string? Search { get; set; }
    public long? EquipmentTypeId { get; set; }
    public long? LocationId { get; set; }
    public bool? IsCentralWarehouse { get; set; }
    public bool? IsActive { get; set; } = true;
    public EquipmentAvailability? Availability { get; set; }
}

public class BackupQuery : PageQuery
{
    public long? RequestedBy { get; set; }
    public WardType? RequestedWard { get; set; }
    public long? EquipmentTypeId { get; set; }
    public BackupRequestStatus? Status { get; set; }
    [MaxLength(50)] public string? BedNumber { get; set; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record EquipmentView(Equipment Equipment, EquipmentAvailability Availability);
public record BackupDetails(BackupRequest Request, AllocationView? Reservation,
    IReadOnlyList<AllocationView> AllocationHistory, IReadOnlyList<BackupEscalation> Escalations,
    DateTime ServerTimeUtc);
public record AllocationView(BackupAllocation Allocation, Equipment Equipment, double RemainingSeconds);
public record BackupActorRequest([Range(1, long.MaxValue)] long PerformedBy);
public record CancelBackupRequest([Range(1, long.MaxValue)] long PerformedBy,
    [Required, MaxLength(500)] string Reason);
