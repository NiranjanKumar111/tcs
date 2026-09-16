using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class ReportsService : IReportsService
{
    private readonly IApplicationRepository database;
    private readonly IAuthenticationService auth;

    public ReportsService(IApplicationRepository database, IAuthenticationService auth)
    {
        this.database = database;
        this.auth = auth;
    }

    public async Task<PagedResult<EquipmentView>> EquipmentAsync(Session actor, EquipmentQuery query)
    {
        await using var db = database.Open();
        var user = await auth.RequireAsync(db, actor, AuthenticationService.Roles);
        IEquipmentService service = new EquipmentService(db, new BackupReservationService(db, TimeProvider.System), TimeProvider.System,
            user.Role == "technician" ? user.Id : null);
        var page = await service.ListAsync(query, default);
        Database.Audit(db, actor.UserId, "Equipment records viewed", "Equipment", 0, AuditAction.view);
        await db.SaveChangesAsync();
        return page;
    }

    public async Task<EquipmentView> TechnicianEquipmentAsync(Session actor, long id)
    {
        await using var db = database.Open();
        await auth.RequireAsync(db, actor, "technician");
        if (!await db.Tickets.AnyAsync(ticket => ticket.EquipmentId == id && ticket.AssignedTo == actor.UserId))
            throw new UnauthorizedAccessException("Equipment not found or not assigned to your tickets.");
        var inventory = new EquipmentService(db, new BackupReservationService(db, TimeProvider.System), TimeProvider.System);
        var result = await inventory.GetAsync(id, default);
        Database.Audit(db, actor.UserId, "Equipment details viewed", "Equipment", id, AuditAction.view);
        await db.SaveChangesAsync();
        return result;
    }

    public async Task<ReferenceData> ReferencesAsync(Session actor)
    {
        await using var db = database.Open();
        await auth.RequireAsync(db, actor, AuthenticationService.Roles);
        var types = await db.EquipmentTypes.Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync();
        var locations = await db.Locations.Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync();
        return new ReferenceData(types, locations);
    }

    public async Task<DashboardReport> DashboardAsync(Session actor, bool exceptions = false)
    {
        await using var db = database.Open();
        await auth.RequireAsync(
            db,
            actor,
            "admin",
            "approver");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var equipment = await db.Equipment.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync();
        var tickets = await db.Tickets.AsNoTracking().Where(x => x.Status != TicketStatus.completed && x.Status != TicketStatus.cancelled).ToListAsync();
        var escalations = await db.BackupEscalations.AsNoTracking().Where(x => x.ResolvedAt == null).ToListAsync();
        var pm = equipment.Where(x => x.MaintenanceFrequency != MaintenanceFrequency.none).ToList();
        var cal = equipment.Where(x => x.CalibrationFrequency != MaintenanceFrequency.none).ToList();
        var verifiedPm = pm.Count(x => x.LastMaintenanceDate != null && x.NextMaintenanceDate >= today);
        var verifiedCal = cal.Count(x => x.LastCalibrationDate != null && x.NextCalibrationDate >= today);
        return new DashboardReport(
            DateTime.UtcNow,
            equipment.Count,
            tickets.Count,
            tickets.Count(x => x.Status == TicketStatus.pending_approval),
            tickets.Count(x => x.DueDate < today),
            escalations.Count,
            pm.Count,
            verifiedPm,
            cal.Count,
            verifiedCal,
            pm.Count(x => x.NextMaintenanceDate < today),
            cal.Count(x => x.NextCalibrationDate < today),
            exceptions,
            exceptions ? equipment.Where(x => x.NextMaintenanceDate < today || x.NextCalibrationDate < today || x.MaintenanceFrequency == MaintenanceFrequency.none || x.LastMaintenanceDate == null).ToList() : [],
            exceptions ? tickets.Where(x => x.DueDate < today || x.Priority == TicketPriority.critical || x.AssignedTo == null || x.ApproverId == null).ToList() : [],
            exceptions ? escalations : []);
    }

    public async Task<IReadOnlyList<AuditLog>> AuditAsync(Session actor)
    {
        await using var db = database.Open();
        await auth.RequireAsync(db, actor, "admin");
        var rows = await db.AuditLogs.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync();
        return rows;
    }
}
