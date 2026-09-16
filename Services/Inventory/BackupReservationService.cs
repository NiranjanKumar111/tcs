using EquipmentManagementBackend.Services;
using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public class BackupReservationService : IBackupReservationService
{
    private readonly IUnitOfWork db;
    private readonly TimeProvider clock;

    public BackupReservationService(IUnitOfWork db, TimeProvider clock)
    {
        this.db = db;
        this.clock = clock;
    }

    public static readonly TimeSpan PickupWindow = TimeSpan.FromMinutes(20);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<BackupDetails> CreateAsync(CreateBackupRequestRequest input, CancellationToken ct)
    {
        EquipmentService.ValidateText(input.BedNumber, 50, "Bed number");
        if (input.Quantity != 1)
        {
            throw new ServiceException(400, "Create one request per equipment item and bed (quantity must be 1).");
        }

        if (!Enum.IsDefined(input.RequestedWard) || !Enum.IsDefined(input.Priority))
        {
            throw new ServiceException(400, "Invalid ward or priority.");
        }

        await using var transaction = await db.BeginInventoryAsync(ct);
        await ExpireWithinTransactionAsync(ct);
        await ValidateUserAsync(input.RequestedBy, ct);
        if (!await db.EquipmentTypes.AnyAsync(x => x.Id == input.EquipmentTypeId && x.IsActive, ct))
        {
            throw new ServiceException(400, "Invalid active equipment type.");
        }

        if (input.RequestedEquipmentId is { } preferred && !await db.Equipment.AnyAsync(x => x.Id == preferred && x.IsActive && x.EquipmentTypeId == input.EquipmentTypeId && x.Location!.IsActive && x.Location.IsCentralWarehouse, ct))
        {
            throw new ServiceException(400, "Requested equipment must match the type and belong to the central warehouse.");
        }

        var request = new BackupRequest
        {
            RequestNumber = $"BR-{Guid.NewGuid():N}",
            RequestedWard = input.RequestedWard,
            BedNumber = input.BedNumber.Trim(),
            RequestedBy = input.RequestedBy,
            EquipmentTypeId = input.EquipmentTypeId,
            RequestedEquipmentId = input.RequestedEquipmentId,
            Quantity = 1,
            Priority = input.Priority,
            Reason = input.Reason ?? "",
            RequestedAt = Now,
            CreatedAt = Now,
            UpdatedAt = Now

        };
        db.BackupRequests.Add(request);
        await db.SaveChangesAsync(ct);
        await ReserveWithinTransactionAsync(request, input.RequestedBy, ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(request.Id, ct);
    }

    public async Task<BackupDetails> ReserveAsync(long id, long actor, CancellationToken ct)
    {
        await using var transaction = await db.BeginInventoryAsync(ct);
        await ExpireWithinTransactionAsync(ct);
        var request = await FindRequestAsync(id, ct);
        await ValidateActorAsync(request, actor, ct);
        if (request.Status != BackupRequestStatus.reserved && request.Status != BackupRequestStatus.in_use)
        {
            if (request.Status is not (BackupRequestStatus.requested or BackupRequestStatus.unreserved or BackupRequestStatus.escalated))
            {
                throw new ServiceException(409, "This request cannot be reserved.");
            }

            await ReserveWithinTransactionAsync(request, actor, ct);
        }

        // Retrying an existing reservation never resets the original deadline.
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task ReserveWithinTransactionAsync(BackupRequest request, long actor, CancellationToken ct)
    {
        var candidates = db.Equipment.Where(e => e.IsActive && e.EquipmentTypeId == request.EquipmentTypeId && e.EquipmentType!.IsActive && e.Location!.IsActive && e.Location.IsCentralWarehouse && !db.Tickets.Any(t => t.EquipmentId == e.Id && t.Status != TicketStatus.completed && t.Status != TicketStatus.cancelled) && !db.BackupAllocations.Any(a => a.EquipmentId == e.Id && (a.Status == BackupAllocationStatus.reserved || a.Status == BackupAllocationStatus.in_use)));
        // A requested item is a preference, not an exclusive restriction. If it is
        // occupied, choose another available item of the same type in the warehouse.
        var equipment = await candidates.OrderByDescending(e => e.Id == request.RequestedEquipmentId).ThenBy(e => e.Id).FirstOrDefaultAsync(ct);
        var now = Now;
        request.UpdatedAt = now;
        if (equipment == null)
        {
            request.Status = BackupRequestStatus.escalated;
            if (!await db.BackupEscalations.AnyAsync(e => e.RequestId == request.Id && e.ResolvedAt == null, ct))
            {
                var admin = await db.Users.Where(u => u.IsActive && u.Role == "admin").OrderBy(u => db.BackupEscalations.Count(e => e.AssignedAdminId == u.Id && e.ResolvedAt == null)).ThenBy(u => u.Id).Select(u => (long? )u.Id).FirstOrDefaultAsync(ct);
                var reason = $"No available central warehouse equipment for type {request.EquipmentTypeId}, ward {request.RequestedWard}, bed {request.BedNumber}.";
                db.BackupEscalations.Add(new() {
                        RequestId = request.Id,
                        AssignedAdminId = admin,
                        Reason = reason,
                        CreatedAt = now
                    });
                if (admin is { } adminId)
                {
                    db.Notifications.Add(new() {
                            RecipientUserId = adminId,
                            TriggeredByUserId = actor,
                            Type = NotificationType.backup,
                            Title = $"Backup request {request.RequestNumber} escalated",
                            Message = reason,
                            CreatedAt = now
                        });
                }
            }
        }
        else
        {
            db.BackupAllocations.Add(new() {
                    RequestId = request.Id,
                    EquipmentId = equipment.Id,
                    AllocatedBy = actor,
                    AllocatedAt = now,
                    ReservationExpiresAt = now.Add(PickupWindow),
                    Status = BackupAllocationStatus.reserved
                });
            request.Status = BackupRequestStatus.reserved;
            await ResolveEscalationsAsync(
                request.Id,
                actor,
                now,
                ct);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<BackupDetails> PickupAsync(
        long requestId,
        long allocationId,
        long actor,
        CancellationToken ct)
    {
        await using var transaction = await db.BeginInventoryAsync(ct);
        await ExpireWithinTransactionAsync(ct);
        var request = await FindRequestAsync(requestId, ct);
        await ValidateActorAsync(request, actor, ct);
        var allocation = await FindAllocationAsync(requestId, allocationId, ct);
        if (allocation.Status != BackupAllocationStatus.in_use)
        {
            if (allocation.Status != BackupAllocationStatus.reserved || allocation.ReservationExpiresAt <= Now)
            {
                await transaction.CommitAsync(ct); // Persist expiry even when the late pickup is rejected.
                throw new ServiceException(409, "Reservation is no longer active. Reserve again before pickup.");
            }

            allocation.Status = BackupAllocationStatus.in_use;
            allocation.PickedUpBy = actor;
            allocation.PickedUpAt = Now;
            request.Status = BackupRequestStatus.in_use;
            request.UpdatedAt = Now;
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return await GetAsync(requestId, ct);
    }

    public async Task<BackupDetails> ReturnAsync(
        long requestId,
        long allocationId,
        long actor,
        CancellationToken ct)
    {
        await using var transaction = await db.BeginInventoryAsync(ct);
        var request = await FindRequestAsync(requestId, ct);
        await ValidateActorAsync(request, actor, ct);
        var allocation = await FindAllocationAsync(requestId, allocationId, ct);
        if (allocation.Status != BackupAllocationStatus.returned)
        {
            if (allocation.Status != BackupAllocationStatus.in_use)
            {
                throw new ServiceException(409, "Only picked-up equipment can be returned.");
            }

            allocation.Status = BackupAllocationStatus.returned;
            allocation.ReturnedBy = actor;
            allocation.ReturnedAt = Now;
            allocation.ReleasedAt = Now;
            request.Status = BackupRequestStatus.returned;
            request.UpdatedAt = Now;
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return await GetAsync(requestId, ct);
    }

    public async Task<BackupDetails> CancelAsync(long id, CancelBackupRequest input, CancellationToken ct)
    {
        EquipmentService.ValidateText(input.Reason, 500, "Cancellation reason");
        await using var transaction = await db.BeginInventoryAsync(ct);
        await ExpireWithinTransactionAsync(ct);
        var request = await FindRequestAsync(id, ct);
        await ValidateActorAsync(request, input.PerformedBy, ct);
        if (request.Status != BackupRequestStatus.cancelled)
        {
            if (request.Status is BackupRequestStatus.in_use or BackupRequestStatus.returned || await db.BackupAllocations.AnyAsync(x => x.RequestId == id && x.Status == BackupAllocationStatus.in_use, ct))
            {
                throw new ServiceException(409, "Picked-up equipment must be returned, not cancelled.");
            }

            var reserved = await db.BackupAllocations.Where(x => x.RequestId == id && x.Status == BackupAllocationStatus.reserved).ToListAsync(ct);
            foreach (var allocation in reserved)
            {
                allocation.Status = BackupAllocationStatus.cancelled;
                allocation.ReleasedAt = Now;
            }

            request.Status = BackupRequestStatus.cancelled;
            request.CancelledBy = input.PerformedBy;
            request.CancelledAt = Now;
            request.CancellationReason = input.Reason.Trim();
            request.UpdatedAt = Now;
            await ResolveEscalationsAsync(
                id,
                input.PerformedBy,
                Now,
                ct);
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<int> ExpireAsync(CancellationToken ct)
    {
        await using var transaction = await db.BeginInventoryAsync(ct);
        var count = await ExpireWithinTransactionAsync(ct);
        await transaction.CommitAsync(ct);
        return count;
    }

    public async Task<int> ExpireWithinTransactionAsync(CancellationToken ct)
    {
        var now = Now;
        var expired = await db.BackupAllocations.Where(a => a.Status == BackupAllocationStatus.reserved && a.ReservationExpiresAt <= now).ToListAsync(ct);
        if (expired.Count == 0)
        {
            return 0;
        }

        var ids = expired.Select(x => x.RequestId).ToArray();
        var requests = await db.BackupRequests.Where(r => ids.Contains(r.Id)).ToListAsync(ct);
        foreach (var allocation in expired)
        {
            allocation.Status = BackupAllocationStatus.expired;
            allocation.ReleasedAt = now;
        }

        foreach (var request in requests)
        {
            request.Status = BackupRequestStatus.unreserved;
            request.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return expired.Count;
    }

    public async Task<BackupDetails> GetAsync(long id, CancellationToken ct)
    {
        await using var transaction = await db.BeginInventoryAsync(ct);
        await ExpireWithinTransactionAsync(ct);
        var request = await db.BackupRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw new ServiceException(404, "Backup request not found.");
        var rows = await (
            from allocation in db.BackupAllocations.AsNoTracking()join equipment in db.Equipment.AsNoTracking().Include(e => e.Location).Include(e => e.EquipmentType)on allocation.EquipmentId equals equipment.Id
            where allocation.RequestId == id
            orderby allocation.Id descending
            select new
            {
                allocation,
                equipment
            }

        ).ToListAsync(ct);
        var now = Now;
        var history = rows.Select(x => new AllocationView(x.allocation, x.equipment, x.allocation.Status == BackupAllocationStatus.reserved ? Math.Max(0, (x.allocation.ReservationExpiresAt - now).TotalSeconds) : 0)).ToList();
        var active = history.FirstOrDefault(x => x.Allocation.Status is BackupAllocationStatus.reserved or BackupAllocationStatus.in_use);
        var escalations = await db.BackupEscalations.AsNoTracking().Where(x => x.RequestId == id).OrderByDescending(x => x.Id).ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new(
            request,
            active,
            history,
            escalations,
            now);
    }

    public async Task<PagedResult<BackupRequest>> ListAsync(BackupQuery query, CancellationToken ct)
    {
        EquipmentService.ValidatePage(query);
        await using var transaction = await db.BeginInventoryAsync(ct);
        await ExpireWithinTransactionAsync(ct);
        var rows = db.BackupRequests.AsNoTracking().AsQueryable();
        if (query.RequestedBy is { } user)
        {
            rows = rows.Where(x => x.RequestedBy == user);
        }

        if (query.RequestedWard is { } ward)
        {
            rows = rows.Where(x => x.RequestedWard == ward);
        }

        if (query.EquipmentTypeId is { } type)
        {
            rows = rows.Where(x => x.EquipmentTypeId == type);
        }

        if (query.Status is { } status)
        {
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.BedNumber))
        {
            rows = rows.Where(x => x.BedNumber == query.BedNumber.Trim());
        }

        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new(
            items,
            total,
            query.Page,
            query.PageSize);
    }

    public async Task<PagedResult<BackupEscalation>> EscalationsAsync(PageQuery query, bool unresolvedOnly, CancellationToken ct)
    {
        EquipmentService.ValidatePage(query);
        var rows = db.BackupEscalations.AsNoTracking().AsQueryable();
        if (unresolvedOnly)
        {
            rows = rows.Where(x => x.ResolvedAt == null);
        }

        var total = await rows.CountAsync(ct);
        return new(
            await rows.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct),
            total,
            query.Page,
            query.PageSize);
    }

    public async Task ValidateUserAsync(long id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == id && x.IsActive, ct))
        {
            throw new ServiceException(400, "Invalid active user.");
        }
    }

    private async Task ValidateActorAsync(BackupRequest request, long actor, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor && x.IsActive, ct) ?? throw new ServiceException(400, "Invalid active user.");
        if (actor != request.RequestedBy && user.Role != "admin")
        {
            throw new ServiceException(403, "Only the requester or an admin can change this reservation.");
        }
    }

    private async Task ResolveEscalationsAsync(
        long id,
        long actor,
        DateTime now,
        CancellationToken ct)
    {
        var escalations = await db.BackupEscalations.Where(x => x.RequestId == id && x.ResolvedAt == null).ToListAsync(ct);
        foreach (var escalation in escalations)
        {
            escalation.ResolvedAt = now;
            escalation.ResolvedBy = actor;
        }
    }

    private async Task<BackupRequest> FindRequestAsync(long id, CancellationToken ct) => await db.BackupRequests.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ServiceException(404, "Backup request not found.");
    private async Task<BackupAllocation> FindAllocationAsync(long requestId, long allocationId, CancellationToken ct) => await db.BackupAllocations.SingleOrDefaultAsync(x => x.Id == allocationId && x.RequestId == requestId, ct) ?? throw new ServiceException(404, "Allocation not found for this request.");
}
