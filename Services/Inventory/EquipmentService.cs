using EquipmentManagementBackend.Services;
using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public class EquipmentService : IEquipmentService
{
    private readonly IUnitOfWork db;
    private readonly IBackupReservationService reservations;
    private readonly TimeProvider clock;
    private readonly long? assignedTechnicianId;

    public EquipmentService(IUnitOfWork db, IBackupReservationService reservations, TimeProvider clock, long? assignedTechnicianId = null)
    {
        this.db = db;
        this.reservations = reservations;
        this.clock = clock;
        this.assignedTechnicianId = assignedTechnicianId;
    }

    public async Task<PagedResult<EquipmentView>> ListAsync(EquipmentQuery query, CancellationToken ct)
    {
        ValidatePage(query);
        var items = db.Equipment.AsNoTracking().Include(x => x.EquipmentType).Include(x => x.Location).AsQueryable();
        if (assignedTechnicianId.HasValue)
            items = items.Where(item => db.Tickets.Any(ticket => ticket.EquipmentId == item.Id && ticket.AssignedTo == assignedTechnicianId));
        if (query.IsActive is { } active)
        {
            items = items.Where(x => x.IsActive == active);
        }

        if (query.EquipmentTypeId is { } type)
        {
            items = items.Where(x => x.EquipmentTypeId == type);
        }

        if (query.LocationId is { } location)
        {
            items = items.Where(x => x.LocationId == location);
        }

        if (query.IsCentralWarehouse is { } warehouse)
        {
            items = items.Where(x => x.Location!.IsCentralWarehouse == warehouse);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            items = items.Where(x => x.Name.ToLower().Contains(search) || x.EquipmentCode.ToLower().Contains(search) || (x.SerialNumber != null && x.SerialNumber.ToLower().Contains(search)));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var views = items.Select(e => new { Equipment = e, Availability = !e.IsActive ? EquipmentAvailability.inactive : db.BackupAllocations.Any(a => a.EquipmentId == e.Id && a.Status == BackupAllocationStatus.in_use) ? EquipmentAvailability.in_use : db.BackupAllocations.Any(a => a.EquipmentId == e.Id && a.Status == BackupAllocationStatus.reserved && a.ReservationExpiresAt > now) ? EquipmentAvailability.reserved : db.Tickets.Any(t => t.EquipmentId == e.Id && t.Status != TicketStatus.completed && t.Status != TicketStatus.cancelled) ? EquipmentAvailability.under_maintenance : EquipmentAvailability.available });
        if (query.Availability is { } availability)
        {
            views = views.Where(x => x.Availability == availability);
        }

        var total = await views.CountAsync(ct);
        var page = await views.OrderBy(x => x.Equipment.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(
            page.Select(x => new EquipmentView(x.Equipment, x.Availability)).ToList(),
            total,
            query.Page,
            query.PageSize);
    }

    public async Task<EquipmentView> GetAsync(long id, CancellationToken ct)
    {
        var equipment = await db.Equipment.AsNoTracking().Include(x => x.Location).Include(x => x.EquipmentType).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ServiceException(404, "Equipment not found.");
        var now = clock.GetUtcNow().UtcDateTime;
        var allocation = await db.BackupAllocations.AsNoTracking().FirstOrDefaultAsync(a => a.EquipmentId == id && (a.Status == BackupAllocationStatus.in_use || (a.Status == BackupAllocationStatus.reserved && a.ReservationExpiresAt > now)), ct);
        var maintenance = await db.Tickets.AnyAsync(t => t.EquipmentId == id && t.Status != TicketStatus.completed && t.Status != TicketStatus.cancelled, ct);
        return new(equipment, !equipment.IsActive ? EquipmentAvailability.inactive : allocation == null ? (maintenance ? EquipmentAvailability.under_maintenance : EquipmentAvailability.available) : allocation.Status == BackupAllocationStatus.in_use ? EquipmentAvailability.in_use : EquipmentAvailability.reserved);
    }

    public async Task<EquipmentView> CreateAsync(CreateEquipmentRequest request, CancellationToken ct)
    {
        ValidateText(request.EquipmentCode, 80, "Equipment code");
        ValidateFields(
            request.Name,
            request.SerialNumber,
            request.Manufacturer,
            request.ModelNumber);
        await ValidateReferencesAsync(
            request.EquipmentTypeId,
            request.LocationId,
            request.CreatedBy,
            ct);
        var code = request.EquipmentCode.Trim();
        if (await db.Equipment.AnyAsync(x => x.EquipmentCode == code, ct))
        {
            throw new ServiceException(409, "Equipment code already exists.");
        }

        var equipment = new Equipment
        {
            EquipmentCode = code,
            Name = request.Name.Trim(),
            EquipmentTypeId = request.EquipmentTypeId,
            LocationId = request.LocationId,
            SerialNumber = request.SerialNumber,
            Manufacturer = request.Manufacturer,
            ModelNumber = request.ModelNumber,
            PurchaseDate = request.PurchaseDate,
            CreatedBy = request.CreatedBy,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            UpdatedAt = clock.GetUtcNow().UtcDateTime

        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync(ct);
        return await GetAsync(equipment.Id, ct);
    }

    public async Task<EquipmentView> UpdateAsync(long id, UpdateEquipmentRequest request, CancellationToken ct)
    {
        ValidateFields(
            request.Name,
            request.SerialNumber,
            request.Manufacturer,
            request.ModelNumber);
        await using var transaction = await db.BeginInventoryAsync(ct);
        await reservations.ExpireWithinTransactionAsync(ct);
        var equipment = await db.Equipment.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ServiceException(404, "Equipment not found.");
        await ValidateReferencesAsync(
            request.EquipmentTypeId,
            request.LocationId,
            request.UpdatedBy,
            ct);
        if (equipment.LocationId != request.LocationId || equipment.EquipmentTypeId != request.EquipmentTypeId || !request.IsActive)
        {
            await EnsureAvailableAsync(id, ct);
        }

        equipment.Name = request.Name.Trim();
        equipment.EquipmentTypeId = request.EquipmentTypeId;
        equipment.LocationId = request.LocationId;
        equipment.SerialNumber = request.SerialNumber;
        equipment.Manufacturer = request.Manufacturer;
        equipment.ModelNumber = request.ModelNumber;
        equipment.PurchaseDate = request.PurchaseDate;
        equipment.IsActive = request.IsActive;
        equipment.UpdatedBy = request.UpdatedBy;
        equipment.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        await using var transaction = await db.BeginInventoryAsync(ct);
        await reservations.ExpireWithinTransactionAsync(ct);
        var equipment = await db.Equipment.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ServiceException(404, "Equipment not found.");
        await EnsureAvailableAsync(id, ct);
        equipment.IsActive = false;
        equipment.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task AssignAsync(
        long id,
        long userId,
        bool technician,
        CancellationToken ct)
    {
        if (!await db.Equipment.AnyAsync(x => x.Id == id && x.IsActive, ct))
        {
            throw new ServiceException(404, "Active equipment not found.");
        }

        await reservations.ValidateUserAsync(userId, ct);
        if (technician)
        {
            if (await db.EquipmentTechnicians.AnyAsync(x => x.EquipmentId == id && x.TechnicianId == userId, ct))
            {
                throw new ServiceException(409, "Technician already assigned.");
            }

            db.EquipmentTechnicians.Add(new() {
                    EquipmentId = id,
                    TechnicianId = userId
                });
        }
        else
        {
            if (await db.EquipmentApprovers.AnyAsync(x => x.EquipmentId == id && x.ApproverId == userId, ct))
            {
                throw new ServiceException(409, "Approver already assigned.");
            }

            db.EquipmentApprovers.Add(new() {
                    EquipmentId = id,
                    ApproverId = userId
                });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureAvailableAsync(long id, CancellationToken ct)
    {
        if (await db.BackupAllocations.AnyAsync(x => x.EquipmentId == id && (x.Status == BackupAllocationStatus.reserved || x.Status == BackupAllocationStatus.in_use), ct))
        {
            throw new ServiceException(409, "Reserved or in-use equipment cannot be deleted, deactivated, moved or change type.");
        }
    }

    private async Task ValidateReferencesAsync(
        long type,
        long location,
        long? user,
        CancellationToken ct)
    {
        if (!await db.EquipmentTypes.AnyAsync(x => x.Id == type && x.IsActive, ct))
        {
            throw new ServiceException(400, "Invalid active equipment type.");
        }

        if (!await db.Locations.AnyAsync(x => x.Id == location && x.IsActive, ct))
        {
            throw new ServiceException(400, "Invalid active location.");
        }

        if (user.HasValue)
        {
            await reservations.ValidateUserAsync(user.Value, ct);
        }
    }

    private static void ValidateFields(
        string name,
        string? serial,
        string? manufacturer,
        string? model)
    {
        ValidateText(name, 200, "Name");
        if (serial?.Length > 120 || manufacturer?.Length > 120 || model?.Length > 120)
        {
            throw new ServiceException(400, "Serial number, manufacturer and model must be at most 120 characters.");
        }
    }

    internal static void ValidateText(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
        {
            throw new ServiceException(400, $"{field} is required and must be at most {max} characters.");
        }
    }

    internal static void ValidatePage(PageQuery query)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
        {
            throw new ServiceException(400, "Invalid page. Page size must be between 1 and 100.");
        }
    }
}
