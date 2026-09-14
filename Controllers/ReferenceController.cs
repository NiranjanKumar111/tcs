using EquipmentManagementBackend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Controllers;

[ApiController]
[Route("api/reference")]
public class ReferenceController(AppDbContext db) : ControllerBase
{
    [HttpGet("equipment-types")] public async Task<IActionResult> EquipmentTypes() => Ok(await db.EquipmentTypes.AsNoTracking().Where(x => x.IsActive).ToListAsync());
    [HttpGet("locations")] public async Task<IActionResult> Locations() => Ok(await db.Locations.AsNoTracking().Where(x => x.IsActive).ToListAsync());
    [HttpGet("users")] public async Task<IActionResult> Users() => Ok(await db.Users.AsNoTracking().Where(x => x.IsActive).ToListAsync());
}
