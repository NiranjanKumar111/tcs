using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class EquipmentManagementService : IEquipmentManagementService
{
    private readonly IApplicationRepository database;
    private readonly IAuthenticationService auth;

    public EquipmentManagementService(IApplicationRepository database, IAuthenticationService auth)
    {
        this.database = database;
        this.auth = auth;
    }

    public async Task<EquipmentAssignees> GetAssigneesAsync(Session actor)
    {
        await using var db = database.Open();
        await auth.RequireAsync(db, actor, "admin");
        var users = await db.Users.AsNoTracking().Where(user => user.IsActive &&
            (user.Role == "technician" || user.Role == "approver"))
            .OrderBy(user => user.Name).ThenBy(user => user.Id).ToListAsync();
        return new EquipmentAssignees(users.Where(user => user.Role == "technician").ToList(),
            users.Where(user => user.Role == "approver").ToList());
    }

    public async Task<ManagedEquipmentDetails> GetEquipmentAsync(Session actor, string idOrCode)
    {
        await using var db = database.Open();
        await auth.RequireAsync(db, actor, "admin");
        var search = idOrCode.Trim();
        long id;
        if (!long.TryParse(search, out id))
        {
            id = await db.Equipment.Where(item => item.EquipmentCode.ToLower() == search.ToLower())
                .Select(item => item.Id).SingleOrDefaultAsync();
        }
        IEquipmentService inventory = new EquipmentService(db, new BackupReservationService(db, TimeProvider.System), TimeProvider.System);
        var result = await inventory.GetAsync(id, default);
        Database.Audit(db, actor.UserId, "Equipment details viewed", "Equipment", id, AuditAction.view);
        await db.SaveChangesAsync();
        var technicians = await (from link in db.EquipmentTechnicians
            join user in db.Users on link.TechnicianId equals user.Id
            where link.EquipmentId == id && link.IsActive
            select user).AsNoTracking().ToListAsync();
        var approvers = await (from link in db.EquipmentApprovers
            join user in db.Users on link.ApproverId equals user.Id
            where link.EquipmentId == id && link.IsActive
            select user).AsNoTracking().ToListAsync();
        return new ManagedEquipmentDetails(result, technicians, approvers);
    }

    public async Task<long> SaveEquipmentAsync(Session actor, long? id, SaveEquipmentInput input)
    {
        InputValidation.RequireText(input.Name, 200, "Equipment name");
        if (!Enum.IsDefined(input.Frequency)) throw new ArgumentException("Invalid frequency.");
        if (input.SerialNumber?.Length > 120 || input.Manufacturer?.Length > 120 || input.ModelNumber?.Length > 120)
            throw new ArgumentException("Serial number, manufacturer and model must be at most 120 characters.");

        await using var db = database.Open();
        // The shared inventory lock protects code generation and assignment changes together.
        await using var transaction = await db.BeginInventoryAsync();
        await auth.RequireAsync(db, actor, "admin");
        await new BackupReservationService(db, TimeProvider.System).ExpireWithinTransactionAsync(default);
        if (!await db.EquipmentTypes.AnyAsync(item => item.Id == input.EquipmentTypeId && item.IsActive) ||
            !await db.Locations.AnyAsync(item => item.Id == input.LocationId && item.IsActive))
            throw new ArgumentException("Choose an active equipment type and location.");
        if (!await db.Users.AnyAsync(user => user.Id == input.TechnicianId && user.IsActive && user.Role == "technician"))
            throw new ArgumentException("Choose an active technician.");
        if (!await db.Users.AnyAsync(user => user.Id == input.ApproverId && user.IsActive && user.Role == "approver"))
            throw new ArgumentException("Choose an active approver.");

        var equipment = id.HasValue
            ? await db.Equipment.FindAsync(id.Value) ?? throw new ArgumentException("Equipment not found.")
            : new Equipment();
        if (id.HasValue && (equipment.LocationId != input.LocationId || equipment.EquipmentTypeId != input.EquipmentTypeId) &&
            (await db.BackupAllocations.AnyAsync(item => item.EquipmentId == id &&
                (item.Status == BackupAllocationStatus.reserved || item.Status == BackupAllocationStatus.in_use)) ||
             await db.Tickets.AnyAsync(ticket => ticket.EquipmentId == id && ticket.Status != TicketStatus.completed && ticket.Status != TicketStatus.cancelled)))
            throw new ArgumentException("Occupied equipment or equipment with open maintenance cannot be moved or change type.");

        if (!id.HasValue)
        {
            equipment.EquipmentCode = await NextEquipmentCodeAsync(db);
            equipment.CreatedBy = actor.UserId;
            equipment.CreatedAt = DateTime.UtcNow;
            equipment.MaintenanceAnchorDate = DateOnly.FromDateTime(equipment.CreatedAt);
            equipment.CalibrationAnchorDate = equipment.MaintenanceAnchorDate;
            db.Equipment.Add(equipment);
        }
        equipment.Name = input.Name.Trim();
        equipment.EquipmentTypeId = input.EquipmentTypeId;
        equipment.LocationId = input.LocationId;
        equipment.SerialNumber = input.SerialNumber ?? equipment.SerialNumber;
        equipment.Manufacturer = input.Manufacturer ?? equipment.Manufacturer;
        equipment.ModelNumber = input.ModelNumber ?? equipment.ModelNumber;
        equipment.UpdatedBy = actor.UserId;
        equipment.UpdatedAt = DateTime.UtcNow;
        equipment.MaintenanceFrequency = input.Frequency;
        equipment.CalibrationFrequency = input.Frequency;
        equipment.MaintenanceAnchorDate ??= DateOnly.FromDateTime(equipment.CreatedAt);
        equipment.CalibrationAnchorDate ??= DateOnly.FromDateTime(equipment.CreatedAt);
        equipment.NextMaintenanceDate = Schedule.Next(equipment.LastMaintenanceDate ?? equipment.MaintenanceAnchorDate.Value, input.Frequency);
        equipment.NextCalibrationDate = Schedule.Next(equipment.LastCalibrationDate ?? equipment.CalibrationAnchorDate.Value, input.Frequency);
        await db.SaveChangesAsync();

        var technicians = await db.EquipmentTechnicians.Where(link => link.EquipmentId == equipment.Id).ToListAsync();
        foreach (var link in technicians) link.IsActive = link.TechnicianId == input.TechnicianId;
        if (!technicians.Any(link => link.TechnicianId == input.TechnicianId))
            db.EquipmentTechnicians.Add(new EquipmentTechnician { EquipmentId = equipment.Id, TechnicianId = input.TechnicianId });
        var approvers = await db.EquipmentApprovers.Where(link => link.EquipmentId == equipment.Id).ToListAsync();
        foreach (var link in approvers) link.IsActive = link.ApproverId == input.ApproverId;
        if (!approvers.Any(link => link.ApproverId == input.ApproverId))
            db.EquipmentApprovers.Add(new EquipmentApprover { EquipmentId = equipment.Id, ApproverId = input.ApproverId });

        Database.Audit(db, actor.UserId, "Equipment, shared frequency and assigned employees saved", "Equipment", equipment.Id,
            id.HasValue ? AuditAction.update : AuditAction.create);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return equipment.Id;
    }

    private static async Task<string> NextEquipmentCodeAsync(IUnitOfWork db)
    {
        var codes = await db.Equipment.Where(item => item.EquipmentCode.StartsWith("EQ-"))
            .Select(item => item.EquipmentCode).ToListAsync();
        long highest = 99;
        foreach (var code in codes)
        {
            if (long.TryParse(code.AsSpan(3), out var number)) highest = Math.Max(highest, number);
        }
        return $"EQ-{checked(highest + 1)}";
    }

    public async Task DeleteEquipmentAsync(Session actor, long id)
    {
        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync();
        await auth.RequireAsync(db, actor, "admin");
        await new BackupReservationService(db, TimeProvider.System).ExpireWithinTransactionAsync(default);
        var equipment = await db.Equipment.FindAsync(id) ?? throw new ArgumentException("Equipment not found.");
        if (await db.BackupAllocations.AnyAsync(x => x.EquipmentId == id && (x.Status == BackupAllocationStatus.reserved || x.Status == BackupAllocationStatus.in_use)) || await db.Tickets.AnyAsync(x => x.EquipmentId == id && x.Status != TicketStatus.completed && x.Status != TicketStatus.cancelled))
        {
            throw new ArgumentException("Return reserved/in-use equipment and close maintenance tickets before deleting.");
        }

        equipment.IsActive = false;
        equipment.UpdatedBy = actor.UserId;
        equipment.UpdatedAt = DateTime.UtcNow;
        Database.Audit(
            db,
            actor.UserId,
            "Equipment soft deleted; history retained",
            "Equipment",
            id,
            AuditAction.delete);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
