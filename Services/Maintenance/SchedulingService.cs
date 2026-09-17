using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class SchedulingService : ISchedulingService
{
    private readonly IApplicationLogger? logger;
    private readonly IApplicationRepository database;
    private readonly IAuthenticationService auth;

    public SchedulingService(IApplicationRepository database, IAuthenticationService auth, IApplicationLogger? logger = null)
    {
        this.logger = logger;
        this.database = database;
        this.auth = auth;
    }

    public Task<int> GenerateAsync(Session actor) => GenerateCoreAsync(actor, default);
    private async Task<int> GenerateCoreAsync(Session? actor, CancellationToken cancellation)
    {
        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(cancellation);
        if (actor != null)
        {
            await auth.RequireAsync(db, actor, "admin");
        }

        var creator = actor?.UserId ?? await db.Users.Where(x => x.IsActive && x.Role == "admin").OrderBy(x => x.Id).Select(x => (long? )x.Id).FirstOrDefaultAsync(cancellation);
        if (creator == null)
        {
            return 0;
        }

        db.ActorId = creator.Value;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var generationCutoff = today.AddDays(2);
        var items = await db.Equipment.Where(x => x.IsActive && (x.NextMaintenanceDate <= generationCutoff || x.NextCalibrationDate <= generationCutoff)).ToListAsync(cancellation);
        int count = 0;
        foreach (var item in items)
        {
            foreach (var type in new[]
                {
                    TicketType.preventive,
                    TicketType.calibration

                }

            )
            {
                var due = type == TicketType.preventive ? item.NextMaintenanceDate : item.NextCalibrationDate;
                var frequency = type == TicketType.preventive ? item.MaintenanceFrequency : item.CalibrationFrequency;
                if (frequency == MaintenanceFrequency.none || due == null || due > generationCutoff)
                {
                    continue;
                }

                if (await db.Tickets.AnyAsync(t => t.EquipmentId == item.Id && t.TicketType == type && t.Status != TicketStatus.completed && t.Status != TicketStatus.cancelled, cancellation))
                {
                    continue;
                }

                var scheduleType = type == TicketType.preventive ? "preventive_maintenance" : "calibration";
                // A completed cycle must never generate another ticket for the same due date.
                if (await db.MaintenanceSchedules.AnyAsync(x => x.EquipmentId == item.Id && x.ScheduleType == scheduleType && x.DueDate == due && x.IsCompleted, cancellation))
                {
                    continue;
                }

                async Task<long?> Assign(string role)
                {
                    var candidates = db.Users.Where(user => user.IsActive && user.Role == role);
                    candidates = role == "technician"
                        ? candidates.Where(user => db.EquipmentTechnicians.Any(link => link.EquipmentId == item.Id && link.TechnicianId == user.Id && link.IsActive))
                        : candidates.Where(user => db.EquipmentApprovers.Any(link => link.EquipmentId == item.Id && link.ApproverId == user.Id && link.IsActive));
                    return await candidates.OrderBy(user => user.Id).Select(user => (long?)user.Id).FirstOrDefaultAsync(cancellation);
                }
                var ticket = new Ticket
                {
                    EquipmentId = item.Id,
                    CreatedById = creator.Value,
                    TicketNumber = "SCH-" + Guid.NewGuid().ToString("N"),
                    TicketType = type,
                    Title = $"Scheduled {type}: {item.Name}",
                    Description = $"Generated from {frequency} frequency; due {due:yyyy-MM-dd}.",
                    DueDate = due.Value,
                    AssignedTo = await MaintenanceService.LeastLoadedAsync(db, "technician"),
                    ApproverId = await Assign("approver"),
                    Priority = due < today ? TicketPriority.high : TicketPriority.medium

                };
                db.Tickets.Add(ticket);
                await db.SaveChangesAsync(cancellation);
                db.MaintenanceSchedules.Add(new MaintenanceSchedule {
                        EquipmentId = item.Id,
                        TicketId = ticket.TicketId,
                        DueDate = due.Value,
                        ScheduleType = scheduleType,
                        CreatedBy = creator
                    });
                db.TicketHistory.Add(new TicketHistory {
                        TicketId = ticket.TicketId,
                        Action = TicketHistoryAction.created,
                        NewStatus = ticket.Status,
                        PerformedBy = creator.Value,
                        Remarks = "Automatically generated from equipment frequency"
                    });
                await db.SaveChangesAsync(cancellation);
                count++;
            }
        }

        // Escalate overdue work once. Preserve workflow state while awaiting approval.
        var overdue = await db.Tickets.Where(t => t.DueDate < today && (t.Status == TicketStatus.open || t.Status == TicketStatus.in_progress || t.Status == TicketStatus.rejected)).ToListAsync(cancellation);
        foreach (var ticket in overdue)
        {
            ticket.Status = TicketStatus.overdue;
            ticket.VersionNumber++;
            ticket.UpdatedAt = DateTime.UtcNow;
            db.TicketHistory.Add(new TicketHistory {
                    TicketId = ticket.TicketId,
                    Action = TicketHistoryAction.status_changed,
                    NewStatus = ticket.Status,
                    PerformedBy = creator.Value,
                    Remarks = "Escalated: maintenance deadline passed; administrator reassignment available"
                });
            Database.Audit(
                db,
                creator.Value,
                "Ticket escalated: maintenance deadline passed",
                "Ticket",
                ticket.TicketId);
        }

        await db.SaveChangesAsync(cancellation);
        await tx.CommitAsync(cancellation);
        return count;
    }

    public async Task RunAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            do
            {
                try
                {
                    await GenerateCoreAsync(null, cancellation);
                }
                catch (OperationCanceledException)when (cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception error)
                {
                    logger?.Error("Schedule processing will retry", error);
                }
            }
            while (await timer.WaitForNextTickAsync(cancellation));
        }
        catch (OperationCanceledException)when (cancellation.IsCancellationRequested)
        {
        }
    }
}
