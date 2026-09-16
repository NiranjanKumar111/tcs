from pathlib import Path
r=Path('ConsoleApp')
p=r/'Services/Administration/Administration.cs';s=p.read_text();at=s.index('    internal static void Text');s=s[:at]+'''    public async Task DeleteEquipmentAsync(Session actor, long id)
    {
        await using var db = database.Open(); await using var tx = await db.BeginInventoryAsync();
        await auth.RequireAsync(db, actor, "admin");
        await new BackupReservationService(db, TimeProvider.System).ExpireWithinTransactionAsync(default);
        var equipment = await db.Equipment.FindAsync(id) ?? throw new ArgumentException("Equipment not found.");
        if (await db.BackupAllocations.AnyAsync(x => x.EquipmentId == id && (x.Status == BackupAllocationStatus.reserved || x.Status == BackupAllocationStatus.in_use)) ||
            await db.Tickets.AnyAsync(x => x.EquipmentId == id && x.Status != TicketStatus.completed && x.Status != TicketStatus.cancelled))
            throw new ArgumentException("Return reserved/in-use equipment and close maintenance tickets before deleting.");
        equipment.IsActive = false; equipment.UpdatedBy = actor.UserId; equipment.UpdatedAt = DateTime.UtcNow;
        Database.Audit(db, actor.UserId, "Equipment soft deleted; history retained", "Equipment", id);
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }

'''+s[at:];p.write_text(s)
p=r/'Interfaces/Services/IAdministrationService.cs';s=p.read_text().replace('public interface IAdministrationService\n{','public interface IAdministrationService\n{\n    Task DeleteEquipmentAsync(Session actor, long id);');p.write_text(s)
(r/'Interfaces/Services/ISchedulingService.cs').write_text('''namespace CriticalCare.ConsoleApp;
public interface ISchedulingService
{
    Task<int> GenerateAsync(Session actor);
    Task RunAsync(CancellationToken cancellation);
}
''')
(r/'Services/Maintenance/Scheduling.cs').write_text('''using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;
namespace CriticalCare.ConsoleApp;

public sealed class Scheduling(IApplicationRepository database, IAuthenticationService auth) : ISchedulingService
{
    public Task<int> GenerateAsync(Session actor) => GenerateCoreAsync(actor, default);
    private async Task<int> GenerateCoreAsync(Session? actor, CancellationToken cancellation)
    {
        await using var db = database.Open(); await using var tx = await db.BeginInventoryAsync(cancellation);
        if (actor != null) await auth.RequireAsync(db, actor, "admin");
        var creator = actor?.UserId ?? await db.Users.Where(x => x.IsActive && x.Role == "admin").OrderBy(x => x.Id).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellation);
        if (creator == null) return 0;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = await db.Equipment.Where(x => x.IsActive && (x.NextMaintenanceDate <= today || x.NextCalibrationDate <= today)).ToListAsync(cancellation);
        int count = 0;
        foreach (var item in items)
        {
            foreach (var type in new[] { TicketType.preventive, TicketType.calibration })
            {
                var due = type == TicketType.preventive ? item.NextMaintenanceDate : item.NextCalibrationDate;
                var frequency = type == TicketType.preventive ? item.MaintenanceFrequency : item.CalibrationFrequency;
                if (frequency == MaintenanceFrequency.none || due == null || due > today) continue;
                if (await db.Tickets.AnyAsync(t => t.EquipmentId == item.Id && t.TicketType == type && t.Status != TicketStatus.completed && t.Status != TicketStatus.cancelled, cancellation)) continue;
                var scheduleType = type == TicketType.preventive ? "preventive_maintenance" : "calibration";
                // A completed cycle must never generate another ticket for the same due date.
                if (await db.MaintenanceSchedules.AnyAsync(x => x.EquipmentId == item.Id && x.ScheduleType == scheduleType && x.DueDate == due && x.IsCompleted, cancellation)) continue;
                async Task<long?> Assign(string role) => await db.Users.Where(x => x.IsActive && x.Role == role)
                    .OrderBy(x => db.Tickets.Count(t => (role == "technician" ? t.AssignedTo == x.Id : t.ApproverId == x.Id) && t.Status != TicketStatus.completed && t.Status != TicketStatus.cancelled))
                    .ThenBy(x => x.Id).Select(x => (long?)x.Id).FirstOrDefaultAsync(cancellation);
                var ticket = new Ticket { EquipmentId = item.Id, CreatedById = creator.Value,
                    TicketNumber = "SCH-" + Guid.NewGuid().ToString("N"), TicketType = type,
                    Title = $"Scheduled {type}: {item.Name}", Description = $"Generated from {frequency} frequency; due {due:yyyy-MM-dd}.",
                    DueDate = due.Value, AssignedTo = await Assign("technician"), ApproverId = await Assign("approver"),
                    Priority = due < today ? TicketPriority.high : TicketPriority.medium };
                db.Tickets.Add(ticket); await db.SaveChangesAsync(cancellation);
                db.MaintenanceSchedules.Add(new MaintenanceSchedule { EquipmentId = item.Id, TicketId = ticket.TicketId, DueDate = due.Value, ScheduleType = scheduleType, CreatedBy = creator });
                db.TicketHistory.Add(new TicketHistory { TicketId = ticket.TicketId, Action = TicketHistoryAction.created, NewStatus = ticket.Status, PerformedBy = creator.Value, Remarks = "Automatically generated from equipment frequency" });
                await db.SaveChangesAsync(cancellation); count++;
            }
        }
        // Escalate overdue work once. Preserve workflow state while awaiting approval.
        var overdue = await db.Tickets.Where(t => t.DueDate < today && (t.Status == TicketStatus.open || t.Status == TicketStatus.in_progress || t.Status == TicketStatus.rejected)).ToListAsync(cancellation);
        foreach (var ticket in overdue)
        {
            ticket.Status = TicketStatus.overdue; ticket.VersionNumber++; ticket.UpdatedAt = DateTime.UtcNow;
            db.TicketHistory.Add(new TicketHistory { TicketId = ticket.TicketId, Action = TicketHistoryAction.status_changed, NewStatus = ticket.Status, PerformedBy = creator.Value, Remarks = "Escalated: maintenance deadline passed; administrator reassignment available" });
            Database.Audit(db, creator.Value, "Ticket escalated: maintenance deadline passed", "Ticket", ticket.TicketId);
        }
        await db.SaveChangesAsync(cancellation); await tx.CommitAsync(cancellation); return count;
    }
    public async Task RunAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            do
            {
                try { await GenerateCoreAsync(null, cancellation); }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
                catch (Exception error) { Console.Error.WriteLine("Schedule processing will retry: " + error.Message); }
            } while (await timer.WaitForNextTickAsync(cancellation));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
    }
}
''')
p=r/'Services/Maintenance/Maintenance.cs';s=p.read_text().replace('        ticket.Status = TicketStatus.completed; ticket.ClosedAt', '''        foreach (var schedule in await db.MaintenanceSchedules.Where(x => x.TicketId == id).ToListAsync())
        { schedule.IsCompleted = true; schedule.UpdatedAt = DateTime.UtcNow; schedule.UpdatedBy = actor.UserId; }
        ticket.Status = TicketStatus.completed; ticket.ClosedAt''');p.write_text(s)
p=r/'Program.cs';s=p.read_text().replace('    var expiry = backups.RunExpiryAsync(shutdown.Token);','''    var scheduling = new Scheduling(database, auth);
    var expiry = backups.RunExpiryAsync(shutdown.Token);
    var scheduler = scheduling.RunAsync(shutdown.Token);''').replace('        var ui = new Terminal(database, auth, new Administration(database, auth), new Maintenance(database, auth), new Reports(database, auth), backups);','''        await using (var seed = database.Open()) await DbSeeder.SeedAsync(seed);
        var admin = new Administration(database, auth);
        var maintenance = new Maintenance(database, auth);
        var reports = new Reports(database, auth);
        var backupScreen = new BackupScreen(auth, backups, reports);
        var ui = new Terminal(auth, new IRoleMenu[] {
            new AdminMenu(auth, admin, maintenance, reports, backups, scheduling, backupScreen),
            new StaffMenu(auth, new IncidentScreen(auth, maintenance, reports), backupScreen),
            new TechnicianMenu(auth, maintenance), new ApproverMenu(auth, maintenance)
        });''').replace('shutdown.Cancel(); await expiry;','shutdown.Cancel(); await Task.WhenAll(expiry, scheduler);');p.write_text(s)
# Record login success/failure without storing email/password text in the audit.
p=r/'Services/Authentication/Authentication.cs';s=p.read_text().replace('            throw new UnauthorizedAccessException("Invalid credentials or inactive account.");','''        {
            db.AuditLogs.Add(new AuditLog { UserId = user?.Id, EntityType = "Session", Description = "Console login failed", Result = EquipmentManagementBackend.Models.Enums.AuditResult.failure });
            await db.SaveChangesAsync();
            throw new UnauthorizedAccessException("Invalid credentials or inactive account.");
        }
        db.AuditLogs.Add(new AuditLog { UserId = user.Id, EntityType = "Session", Description = "Console login succeeded" });
        await db.SaveChangesAsync();''');p.write_text(s)
