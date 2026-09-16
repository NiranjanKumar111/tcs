using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Controllers;

[ApiController]
[Route("api/maintenance-schedules")]
public class MaintenanceSchedulesController(AppDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> GetAll() => Ok(await db.MaintenanceSchedules.AsNoTracking().OrderBy(x => x.DueDate).ToListAsync());
    [HttpPost] public async Task<IActionResult> Create(MaintenanceSchedule x) { x.Id = 0; x.CreatedAt = DateTime.UtcNow; x.UpdatedAt = DateTime.UtcNow; db.MaintenanceSchedules.Add(x); await db.SaveChangesAsync(); return Ok(x); }
}
