using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentManagementBackend.Controllers;

[ApiController]
[Route("api/equipment")]
public class EquipmentController(EquipmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] EquipmentQuery query, CancellationToken ct) => Ok(await service.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateEquipmentRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Equipment.Id }, result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateEquipmentRequest request, CancellationToken ct) => Ok(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/technicians")]
    public async Task<IActionResult> AddTechnician(long id, AssignEquipmentUserRequest request, CancellationToken ct)
    {
        await service.AssignAsync(id, request.UserId, true, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/approvers")]
    public async Task<IActionResult> AddApprover(long id, AssignEquipmentUserRequest request, CancellationToken ct)
    {
        await service.AssignAsync(id, request.UserId, false, ct);
        return NoContent();
    }
}
