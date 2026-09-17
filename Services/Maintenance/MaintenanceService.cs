using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class MaintenanceService : IMaintenanceService
{
    private readonly IApplicationRepository database;
    private readonly IAuthenticationService auth;

    public MaintenanceService(IApplicationRepository database, IAuthenticationService auth)
    {
        this.database = database;
        this.auth = auth;
    }

    private static bool IsTicketOpen(Ticket ticket) => ticket.Status is not (TicketStatus.completed or TicketStatus.cancelled);
    public Task<long> CreateAsync(Session actor, long equipmentId, TicketType type) =>
        CreateCoreAsync(actor, equipmentId, type,
            type == TicketType.calibration ? TicketPriority.low : TicketPriority.medium,
            $"{type} ticket for equipment {equipmentId}", "", default, null, null, true);

    public Task<long> CreateAsync(Session actor, long equipmentId, TicketType type, TicketPriority priority,
        string title, string description, DateOnly due, long? technician = null, long? approver = null) =>
        CreateCoreAsync(actor, equipmentId, type, priority, title, description, due, technician, approver, false);

    private async Task<long> CreateCoreAsync(
        Session actor,
        long equipmentId,
        TicketType type,
        TicketPriority priority,
        string title,
        string description,
        DateOnly due,
        long? technician = null,
        long? approver = null, bool automatic = false)
    {
        InputValidation.RequireText(title, 250, "Title");
        if (!Enum.IsDefined(type) || !Enum.IsDefined(priority))
        {
            throw new ArgumentException("Invalid ticket type or priority.");
        }

        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        var user = await auth.RequireAsync(
            db,
            actor,
            "admin",
            "staff");
        if (user.Role == "staff" && (technician != null || approver != null))
        {
            throw new UnauthorizedAccessException("Staff ticket assignment is automatic.");
        }

        if (!await db.Equipment.AnyAsync(x => x.Id == equipmentId && x.IsActive))
        {
            throw new ArgumentException("Active equipment not found.");
        }

        technician ??= await LeastLoadedAsync(db, "technician");
        approver ??= await (from link in db.EquipmentApprovers
            join employee in db.Users on link.ApproverId equals employee.Id
            where link.EquipmentId == equipmentId && link.IsActive && employee.IsActive && employee.Role == "approver"
            orderby employee.Id select (long?)employee.Id).FirstOrDefaultAsync();
        approver ??= await LeastLoadedAsync(db, "approver");
        await ValidateAssigneeAsync(db, technician, "technician");
        await ValidateAssigneeAsync(db, approver, "approver");
        var registeredAt = DateTime.UtcNow;
        var ticket = new Ticket
        {
            EquipmentId = equipmentId,
            CreatedById = actor.UserId,
            TicketType = type,
            Priority = priority,
            Title = title.Trim(),
            Description = description,
            CreatedAt = registeredAt,
            DueDate = automatic ? DateOnly.FromDateTime(registeredAt).AddDays(3) : due,
            AssignedTo = technician,
            ApproverId = approver,
            TicketNumber = "TKT-" + Guid.NewGuid().ToString("N")

        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        AddTicketHistory(
            db,
            ticket,
            actor.UserId,
            TicketHistoryAction.created,
            "Ticket created");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ticket.TicketId;
    }

    public async Task ReassignTechnicianAsync(Session actor, long ticketId, int version, long technician)
    {
        await using var db = database.Open();
        await using var transaction = await db.BeginInventoryAsync();
        await auth.RequireAsync(db, actor, "admin");
        var ticket = await FindTicketAsync(db, ticketId, version);
        if (!IsTicketOpen(ticket) || ticket.Status == TicketStatus.approved)
        {
            throw new ArgumentException("Cannot reassign a closed or approved ticket.");
        }
        await ValidateAssigneeAsync(db, technician, "technician");
        var previousTechnician = ticket.AssignedTo;
        ticket.AssignedTo = technician;
        MarkTicketChanged(ticket);
        AddTicketHistory(db, ticket, actor.UserId, TicketHistoryAction.reassigned,
            $"Technician changed from {previousTechnician} to {technician}; approver unchanged");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task AssignAsync(
        Session actor,
        long ticketId,
        int version,
        long technician,
        long approver)
    {
        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        await auth.RequireAsync(db, actor, "admin");
        var ticket = await FindTicketAsync(db, ticketId, version);
        if (!IsTicketOpen(ticket) || ticket.Status == TicketStatus.approved)
        {
            throw new ArgumentException("Cannot reassign a closed or approved ticket.");
        }

        await ValidateAssigneeAsync(db, technician, "technician");
        await ValidateAssigneeAsync(db, approver, "approver");
        ticket.AssignedTo = technician;
        ticket.ApproverId = approver;
        MarkTicketChanged(ticket);
        AddTicketHistory(
            db,
            ticket,
            actor.UserId,
            TicketHistoryAction.reassigned,
            "Assignments updated");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task ProgressAsync(
        Session actor,
        long id,
        int version,
        string notes)
    {
        InputValidation.RequireText(notes, 4000, "Work notes");
        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        await auth.RequireAsync(db, actor, "technician");
        var ticket = await FindTicketAsync(db, id, version);
        RequireTechnician(ticket, actor);
        if (ticket.Status is not (TicketStatus.open or TicketStatus.in_progress or TicketStatus.rejected or TicketStatus.overdue))
        {
            throw new ArgumentException("Work is locked while awaiting approval or after approval/closure.");
        }

        if (await db.BackupAllocations.AnyAsync(x => x.EquipmentId == ticket.EquipmentId && (x.Status == BackupAllocationStatus.reserved || x.Status == BackupAllocationStatus.in_use)))
        {
            throw new ArgumentException("Return or release the backup allocation before starting maintenance.");
        }

        ticket.Status = TicketStatus.in_progress;
        ticket.WorkReport = notes.Trim();
        MarkTicketChanged(ticket);
        AddTicketHistory(
            db,
            ticket,
            actor.UserId,
            TicketHistoryAction.status_changed,
            notes);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task ChecklistAsync(
        Session actor,
        long id,
        int version,
        string description,
        bool passed,
        string notes)
    {
        InputValidation.RequireText(description, 250, "Checklist item");
        if (notes.Length > 1000)
        {
            throw new ArgumentException("Notes must be at most 1000 characters.");
        }

        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        await auth.RequireAsync(db, actor, "technician");
        var ticket = await FindTicketAsync(db, id, version);
        RequireTechnician(ticket, actor);
        if (ticket.Status != TicketStatus.in_progress)
        {
            throw new ArgumentException("Start work before editing the checklist.");
        }

        var item = await db.TicketComplianceItems.FirstOrDefaultAsync(x => x.TicketId == id && x.Description == description.Trim());
        if (item == null)
        {
            item = new TicketComplianceItem
            {
                TicketId = id,
                Description = description.Trim()

            };
            db.TicketComplianceItems.Add(item);
        }

        item.Passed = passed;
        item.Notes = notes;
        MarkTicketChanged(ticket);
        AddTicketHistory(
            db,
            ticket,
            actor.UserId,
            TicketHistoryAction.comment_added,
            "Checklist: " + description);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task CalibrationAsync(
        Session actor,
        long id,
        int version,
        string parameter,
        string unit,
        decimal minimum,
        decimal maximum,
        decimal measured)
    {
        InputValidation.RequireText(parameter, 150, "Parameter");
        InputValidation.RequireText(unit, 50, "Unit");
        if (minimum > maximum)
        {
            throw new ArgumentException("Minimum cannot exceed maximum.");
        }

        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        await auth.RequireAsync(db, actor, "technician");
        var ticket = await FindTicketAsync(db, id, version);
        RequireTechnician(ticket, actor);
        if (ticket.Status != TicketStatus.in_progress || ticket.TicketType != TicketType.calibration)
        {
            throw new ArgumentException("An in-progress calibration ticket is required.");
        }

        var result = await db.TicketCalibrationResults.FirstOrDefaultAsync(x => x.TicketId == id && x.Parameter == parameter.Trim());
        if (result == null)
        {
            result = new TicketCalibrationResult
            {
                TicketId = id,
                Parameter = parameter.Trim()

            };
            db.TicketCalibrationResults.Add(result);
        }

        result.Unit = unit;
        result.Minimum = minimum;
        result.Maximum = maximum;
        result.Measured = measured;
        result.Passed = measured >= minimum && measured <= maximum;
        MarkTicketChanged(ticket);
        AddTicketHistory(
            db,
            ticket,
            actor.UserId,
            TicketHistoryAction.comment_added,
            "Calibration reading: " + parameter);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task SubmitAsync(
        Session actor,
        long id,
        int version,
        DateOnly completedOn)
    {
        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        await auth.RequireAsync(db, actor, "technician");
        var ticket = await FindTicketAsync(db, id, version);
        RequireTechnician(ticket, actor);
        if (ticket.Status != TicketStatus.in_progress || string.IsNullOrWhiteSpace(ticket.WorkReport))
        {
            throw new ArgumentException("An in-progress ticket with a work report is required.");
        }

        if (completedOn > DateOnly.FromDateTime(DateTime.UtcNow) || completedOn < DateOnly.FromDateTime(ticket.CreatedAt))
        {
            throw new ArgumentException("Completion date must be between ticket creation and today (UTC).");
        }

        await ValidateEvidenceAsync(db, ticket);
        await ValidateAssigneeAsync(db, ticket.ApproverId, "approver");
        if (ticket.ApproverId == null)
        {
            throw new ArgumentException("Ask the admin to assign an approver first.");
        }

        ticket.WorkCompletedOn = completedOn;
        ticket.Status = TicketStatus.pending_approval;
        ticket.SubmittedAt = DateTime.UtcNow;
        MarkTicketChanged(ticket);
        AddTicketHistory(
            db,
            ticket,
            actor.UserId,
            TicketHistoryAction.status_changed,
            "Submitted for compliance review");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task ReviewAsync(
        Session actor,
        long id,
        int version,
        bool approve,
        string notes)
    {
        InputValidation.RequireText(notes, 2000, "Review notes");
        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        await auth.RequireAsync(db, actor, "approver");
        var ticket = await FindTicketAsync(db, id, version);
        RequireApprover(ticket, actor);
        if (ticket.Status != TicketStatus.pending_approval)
        {
            throw new ArgumentException("Only pending approvals can be reviewed.");
        }

        if (approve)
        {
            await ValidateEvidenceAsync(db, ticket);
        }

        ticket.Status = approve ? TicketStatus.approved : TicketStatus.rejected;
        ticket.ReviewedAt = DateTime.UtcNow;
        MarkTicketChanged(ticket);
        db.TicketComplianceVerifications.Add(new() {
                TicketId = id,
                ApproverId = actor.UserId,
                Approved = approve,
                Notes = notes,
                TicketVersion = ticket.VersionNumber
            });
        AddTicketHistory(
            db,
            ticket,
            actor.UserId,
            approve ? TicketHistoryAction.approved : TicketHistoryAction.rejected,
            notes);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task CloseAsync(Session actor, long id, int version)
    {
        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        await auth.RequireAsync(db, actor, "approver");
        var ticket = await FindTicketAsync(db, id, version);
        RequireApprover(ticket, actor);
        if (ticket.Status != TicketStatus.approved || ticket.WorkCompletedOn == null)
        {
            throw new ArgumentException("Only an approved, completed work report can be closed.");
        }

        var equipment = await db.Equipment.SingleAsync(x => x.Id == ticket.EquipmentId);
        var completed = ticket.WorkCompletedOn.Value;
        if (ticket.TicketType == TicketType.preventive && (equipment.LastMaintenanceDate == null || completed > equipment.LastMaintenanceDate))
        {
            equipment.LastMaintenanceDate = completed;
            equipment.NextMaintenanceDate = Schedule.Next(completed, equipment.MaintenanceFrequency);
        }

        if (ticket.TicketType == TicketType.calibration && (equipment.LastCalibrationDate == null || completed > equipment.LastCalibrationDate))
        {
            equipment.LastCalibrationDate = completed;
            equipment.NextCalibrationDate = Schedule.Next(completed, equipment.CalibrationFrequency);
        }

        equipment.UpdatedAt = DateTime.UtcNow;
        equipment.UpdatedBy = actor.UserId;
        foreach (var schedule in await db.MaintenanceSchedules.Where(x => x.TicketId == id).ToListAsync())
        {
            schedule.IsCompleted = true;
            schedule.UpdatedAt = DateTime.UtcNow;
            schedule.UpdatedBy = actor.UserId;
        }

        ticket.Status = TicketStatus.completed;
        ticket.ClosedAt = DateTime.UtcNow;
        MarkTicketChanged(ticket);
        AddTicketHistory(
            db,
            ticket,
            actor.UserId,
            TicketHistoryAction.completed,
            "Closed after approval; applicable compliance schedule updated");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<List<Ticket>> ListAsync(Session actor, bool pendingOnly = false, long? equipmentId = null)
    {
        await using var db = database.Open();
        var user = await auth.RequireAsync(db, actor, AuthenticationService.Roles);
        var query = GetVisibleTickets(db, user);
        if (pendingOnly)
        {
            query = query.Where(x => x.Status == TicketStatus.pending_approval);
        }

        if (equipmentId.HasValue)
        {
            query = query.Where(x => x.EquipmentId == equipmentId);
        }

        return await query.AsNoTracking().OrderByDescending(x => x.TicketId).ToListAsync();
    }

    public async Task<TicketDetails> DetailAsync(Session actor, long id)
    {
        await using var db = database.Open();
        var user = await auth.RequireAsync(db, actor, AuthenticationService.Roles);
        var ticket = await GetVisibleTickets(db, user).AsNoTracking().SingleOrDefaultAsync(x => x.TicketId == id) ?? throw new UnauthorizedAccessException("Ticket not found or not visible to your account.");
        var history = await db.TicketHistory.Where(x => x.TicketId == id).OrderBy(x => x.Id).ToListAsync();
        var checklist = await db.TicketComplianceItems.Where(x => x.TicketId == id).ToListAsync();
        var readings = await db.TicketCalibrationResults.Where(x => x.TicketId == id).ToListAsync();
        var reviews = await db.TicketComplianceVerifications.Where(x => x.TicketId == id).OrderBy(x => x.Id).ToListAsync();
        return new TicketDetails(
            ticket,
            history,
            checklist,
            readings,
            reviews,
            await db.Users.Where(user => user.Id == ticket.AssignedTo).Select(user => user.Name).SingleOrDefaultAsync(),
            await db.Users.Where(user => user.Id == ticket.ApproverId).Select(user => user.Name).SingleOrDefaultAsync());
    }

    private static IQueryable<Ticket> GetVisibleTickets(IUnitOfWork db, User user) => user.Role switch
    {
        "admin" => db.Tickets,
        "approver" => db.Tickets.Where(x => x.ApproverId == user.Id),
        "technician" => db.Tickets.Where(x => x.AssignedTo == user.Id),
        _ => db.Tickets.Where(x => x.CreatedById == user.Id)};
    private static async Task ValidateEvidenceAsync(IUnitOfWork db, Ticket ticket)
    {
        if (!await db.TicketComplianceItems.AnyAsync(x => x.TicketId == ticket.TicketId) || await db.TicketComplianceItems.AnyAsync(x => x.TicketId == ticket.TicketId && !x.Passed))
        {
            throw new ArgumentException("Record at least one checklist item and pass all checks before submission/approval.");
        }

        if (ticket.TicketType == TicketType.calibration && (!await db.TicketCalibrationResults.AnyAsync(x => x.TicketId == ticket.TicketId) || await db.TicketCalibrationResults.AnyAsync(x => x.TicketId == ticket.TicketId && !x.Passed)))
        {
            throw new ArgumentException("Calibration requires recorded measurements within their stated limits.");
        }
    }

    private static async Task<Ticket> FindTicketAsync(IUnitOfWork db, long id, int version)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(x => x.TicketId == id) ?? throw new ArgumentException("Ticket not found.");
        if (ticket.VersionNumber != version)
        {
            throw new InvalidOperationException("Ticket changed. Refresh and use its latest version.");
        }

        return ticket;
    }

    private static void MarkTicketChanged(Ticket ticket)
    {
        ticket.VersionNumber++;
        ticket.UpdatedAt = DateTime.UtcNow;
    }

    private static void RequireTechnician(Ticket ticket, Session actor)
    {
        if (ticket.AssignedTo != actor.UserId)
        {
            throw new UnauthorizedAccessException("Only the assigned technician can change this ticket.");
        }
    }

    private static void RequireApprover(Ticket ticket, Session actor)
    {
        if (ticket.ApproverId != actor.UserId || ticket.AssignedTo == actor.UserId)
        {
            throw new UnauthorizedAccessException("Only the assigned, independent approver can review or close this ticket.");
        }
    }

    private static void AddTicketHistory(
        IUnitOfWork db,
        Ticket ticket,
        long actor,
        TicketHistoryAction action,
        string notes)
    {
        db.TicketHistory.Add(new() {
                TicketId = ticket.TicketId,
                Action = action,
                NewStatus = ticket.Status,
                PerformedBy = actor,
                Remarks = notes
            });
        Database.Audit(
            db,
            actor,
            $"Ticket {action}: {ticket.Status}",
            "Ticket",
            ticket.TicketId);
    }

    private static async Task ValidateAssigneeAsync(IUnitOfWork db, long? id, string role)
    {
        if (id.HasValue && !await db.Users.AnyAsync(x => x.Id == id && x.IsActive && x.Role == role))
        {
            throw new ArgumentException($"Selected {role} is invalid or inactive.");
        }
    }

    internal static Task<long?> LeastLoadedAsync(IUnitOfWork db, string role) => db.Users.Where(x => x.IsActive && x.Role == role).OrderBy(x => db.Tickets.Count(t => (role == "technician" ? t.AssignedTo == x.Id : t.ApproverId == x.Id) && t.Status != TicketStatus.completed && t.Status != TicketStatus.cancelled)).ThenBy(x => x.Id).Select(x => (long? )x.Id).FirstOrDefaultAsync();
}
