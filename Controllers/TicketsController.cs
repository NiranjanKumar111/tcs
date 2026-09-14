using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Controllers;

[ApiController]
[Route("api/tickets")]
public class TicketsController(AppDbContext db, AssignmentService assignments) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await db.Tickets.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync());

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var t = await db.Tickets.AsNoTracking().Include(x => x.Equipment).FirstOrDefaultAsync(x => x.TicketId == id);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTicketRequest r)
    {
        if (!await db.Equipment.AnyAsync(x => x.Id == r.EquipmentId && x.IsActive)) return BadRequest("Invalid or inactive equipment");

        var tech = await assignments.GetLeastLoadedTechnicianAsync(r.EquipmentId);
        var approver = await assignments.GetLeastLoadedApproverAsync(r.EquipmentId);
        var ticket = new Ticket
        {
            TicketNumber = $"TKT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100,999)}",
            EquipmentId = r.EquipmentId, CreatedById = r.CreatedById, Title = r.Title,
            Description = r.Description, Priority = r.Priority, DueDate = r.DueDate,
            AssignedTo = tech, ApproverId = approver
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        db.TicketHistory.Add(new TicketHistory { TicketId = ticket.TicketId, Action = TicketHistoryAction.created, NewStatus = ticket.Status, NewAssignedTo = tech, PerformedBy = r.CreatedById });
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = ticket.TicketId }, ticket);
    }

    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, UpdateTicketStatusRequest r)
    {
        var t = await db.Tickets.FirstOrDefaultAsync(x => x.TicketId == id); if (t is null) return NotFound();
        if (t.VersionNumber != r.VersionNumber) return Conflict(new { message = "Ticket was modified by another user.", currentVersion = t.VersionNumber });
        t.Status = r.Status; t.VersionNumber++; t.UpdatedAt = DateTime.UtcNow;
        db.TicketHistory.Add(new TicketHistory { TicketId = id, Action = TicketHistoryAction.status_changed, NewStatus = r.Status, Remarks = r.Remarks, PerformedBy = r.PerformedBy });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict("Optimistic concurrency conflict."); }
        return Ok(t);
    }

    [HttpGet("{id:long}/history")]
    public async Task<IActionResult> History(long id) => Ok(await db.TicketHistory.AsNoTracking().Where(x => x.TicketId == id).OrderBy(x => x.PerformedAt).ToListAsync());
}
