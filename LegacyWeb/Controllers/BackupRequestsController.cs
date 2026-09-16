using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentManagementBackend.Controllers;

[ApiController]
[Route("api/backup-requests")]
public class BackupRequestsController(BackupReservationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] BackupQuery query, CancellationToken ct) => Ok(await service.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    [HttpGet("{id:long}/reservation")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateBackupRequestRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Request.Id }, result);
    }

    [HttpPost("{id:long}/reserve")]
    public async Task<IActionResult> Reserve(long id, BackupActorRequest request, CancellationToken ct) =>
        Ok(await service.ReserveAsync(id, request.PerformedBy, ct));

    [HttpPost("{id:long}/allocations/{allocationId:long}/pickup")]
    public async Task<IActionResult> Pickup(long id, long allocationId, BackupActorRequest request, CancellationToken ct) =>
        Ok(await service.PickupAsync(id, allocationId, request.PerformedBy, ct));

    [HttpPost("{id:long}/allocations/{allocationId:long}/return")]
    public async Task<IActionResult> Return(long id, long allocationId, BackupActorRequest request, CancellationToken ct) =>
        Ok(await service.ReturnAsync(id, allocationId, request.PerformedBy, ct));

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancelBackupRequest request, CancellationToken ct) =>
        Ok(await service.CancelAsync(id, request, ct));

    [HttpGet("escalations")]
    public async Task<IActionResult> Escalations([FromQuery] PageQuery query, CancellationToken ct, [FromQuery] bool unresolvedOnly = true) =>
        Ok(await service.EscalationsAsync(query, unresolvedOnly, ct));
}
