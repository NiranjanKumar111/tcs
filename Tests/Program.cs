using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

if (args.Contains("--seed-local-demo"))
{
    await LocalDatabaseDemo.RunAsync();
    return;
}

// Each run gets its own schema. The caller supplies an isolated PostgreSQL database.
var connection = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION")
    ?? "Host=localhost;Port=55438;Database=reservation_tests;Username=postgres;Password=reservation_test_only";
var schema = "test_" + Guid.NewGuid().ToString("N");
await using var admin = new NpgsqlConnection(connection);
await admin.OpenAsync();
await using (var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await command.ExecuteNonQueryAsync();
var builder = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema };
var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString,
    postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options;
var clock = new TestClock();
var checks = 0;
AppDbContext Context() => new(options);
BackupReservationService Backup(AppDbContext db) => new(db, clock);
EquipmentService EquipmentServiceFor(AppDbContext db) => new(db, Backup(db), clock);
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
    checks++; Console.WriteLine("PASS: " + message);
}
async Task Reject(int status, Func<Task> action, string message)
{
    try { await action(); } catch (ServiceException e) when (e.StatusCode == status) { Check(true, message); return; }
    throw new Exception("FAILED: " + message);
}
async Task<BackupDetails> Create(long user = 2, long type = 1, string bed = "B-1")
{
    await using var db = Context();
    return await Backup(db).CreateAsync(new(WardType.ICU, user, type, bed), default);
}
try
{
    await using (var db = Context())
    {
        // Exercise both migrations, including upgrading existing occupied equipment.
        await db.GetService<IMigrator>().MigrateAsync("20260914160344_InitialSchema");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO users ("Id", "Name", "Email", "Role", "IsActive", "CreatedAt", "UpdatedAt") VALUES
                (1,'Admin','admin@test','admin',TRUE,now(),now()),
                (2,'Staff A','a@test','user',TRUE,now(),now()),
                (3,'Staff B','b@test','user',TRUE,now(),now());
            INSERT INTO equipment_types ("Id", "TypeCode", "TypeName", "IsActive") VALUES (1,'VENT','Ventilator',TRUE),(2,'PUMP','Pump',TRUE);
            INSERT INTO locations ("Id", "Code", "Name", "IsActive", "CreatedAt", "UpdatedAt") VALUES
                (1,'CENTRAL-WAREHOUSE','Warehouse',TRUE,now(),now()), (2,'ICU','ICU',TRUE,now(),now());
            INSERT INTO equipment ("Id","EquipmentCode","Name","EquipmentTypeId","LocationId","IsActive","CreatedAt","UpdatedAt")
                VALUES (100,'LEGACY','Legacy occupied equipment',2,1,TRUE,now(),now());
            INSERT INTO backup_requests ("Id","RequestNumber","RequestedWard","RequestedBy","EquipmentTypeId","Quantity","Priority","Reason","Status","RequestedAt","CreatedAt","UpdatedAt")
                VALUES (100,'LEGACY','ICU',2,2,1,'medium','','allocated',now(),now(),now());
            INSERT INTO backup_allocations ("RequestId","EquipmentId","AllocatedBy","AllocatedAt") VALUES (100,100,1,now());
            """);
        await db.Database.MigrateAsync();
        Check(await db.BackupAllocations.AnyAsync(x => x.EquipmentId == 100 && x.Status == BackupAllocationStatus.in_use), "migration preserves legacy occupied equipment");
        Check(await db.Locations.AnyAsync(x => x.Id == 1 && x.IsCentralWarehouse), "migration recognizes central warehouse");
        // Original DbContext enum converter must construct correctly at runtime.
        Check(await db.BackupRequests.AnyAsync(x => x.Id == 100 && x.Status == BackupRequestStatus.in_use), "legacy request state is upgraded");
    }

    long equipmentId;
    await using (var db = Context())
    {
        var service = EquipmentServiceFor(db);
        var item = await service.CreateAsync(new("VENT-1", "Ventilator One", 1, 1, "SN-1", null, null, null, 1), default);
        equipmentId = item.Equipment.Id;
        Check(item.Availability == EquipmentAvailability.available, "create equipment starts available");
        await service.CreateAsync(new("WARD-VENT", "Ward Ventilator", 1, 2, null, null, null, null, 1), default);
        var page = await service.ListAsync(new() { Search = "vent", IsCentralWarehouse = true, Availability = EquipmentAvailability.available, PageSize = 1 }, default);
        Check(page.TotalCount == 1 && page.Items.Single().Equipment.Id == equipmentId, "equipment filters, availability projection and pagination");
        await Reject(400, () => service.ListAsync(new() { Page = 0 }, default), "reject invalid pagination");
        await Reject(400, () => service.CreateAsync(new("", "name", 1, 1, null, null, null, null, 1), default), "reject empty equipment code");
        await Reject(409, () => service.CreateAsync(new("VENT-1", "duplicate", 1, 1, null, null, null, null, 1), default), "reject duplicate equipment code");
        await service.UpdateAsync(equipmentId, new("Updated Ventilator", 1, 1, "SN-1", null, null, null, true, 1), default);
        Check((await service.GetAsync(equipmentId, default)).Equipment.Name == "Updated Ventilator", "edit equipment through service");
    }

    var concurrent = await Task.WhenAll(Create(2), Create(3));
    var reserved = concurrent.Single(x => x.Request.Status == BackupRequestStatus.reserved);
    var escalated = concurrent.Single(x => x.Request.Status == BackupRequestStatus.escalated);
    var allocationId = reserved.Reservation!.Allocation.Id;
    Check(concurrent.Count(x => x.Reservation != null) == 1, "simultaneous staff requests cannot reserve the same equipment");
    Check(escalated.Escalations.Count == 1 && escalated.Escalations[0].AssignedAdminId == 1, "unavailable equipment creates an admin escalation");
    Check(reserved.Reservation!.Allocation.ReservationExpiresAt - reserved.Reservation.Allocation.AllocatedAt == TimeSpan.FromMinutes(20), "reservation window is exactly 20 minutes");
    await using (var db = Context())
    {
        db.BackupAllocations.Add(new BackupAllocation
        {
            RequestId = escalated.Request.Id, EquipmentId = equipmentId, AllocatedBy = escalated.Request.RequestedBy,
            Status = BackupAllocationStatus.reserved, AllocatedAt = clock.GetUtcNow().UtcDateTime,
            ReservationExpiresAt = clock.GetUtcNow().UtcDateTime.AddMinutes(20)
        });
        var rejected = false;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { rejected = true; }
        Check(rejected, "database unique index prevents duplicate allocation even outside service");
    }
    await using (var db = Context())
    {
        await Reject(409, () => EquipmentServiceFor(db).DeleteAsync(equipmentId, default), "cannot delete reserved equipment");
    }
    await using (var db = Context())
    {
        var retried = await Backup(db).ReserveAsync(reserved.Request.Id, reserved.Request.RequestedBy, default);
        Check(retried.Reservation!.Allocation.Id == allocationId && retried.Reservation.Allocation.ReservationExpiresAt == reserved.Reservation.Allocation.ReservationExpiresAt,
            "retry reserve does not extend deadline or allocate twice");
    }
    await using (var db = Context())
    {
        var retry = await Backup(db).ReserveAsync(escalated.Request.Id, escalated.Request.RequestedBy, default);
        Check(retry.Escalations.Count == 1, "repeated shortage does not duplicate escalation");
    }
    await using (var db = Context())
    {
        var other = reserved.Request.RequestedBy == 2 ? 3 : 2;
        await Reject(403, () => Backup(db).PickupAsync(reserved.Request.Id, allocationId, other, default), "other staff cannot pick up requester reservation");
    }

    clock.Advance(TimeSpan.FromMinutes(20));
    await using (var db = Context())
    {
        await Reject(409, () => Backup(db).PickupAsync(reserved.Request.Id, allocationId, reserved.Request.RequestedBy, default), "pickup at exact expiry is rejected");
    }
    await using (var db = Context())
    {
        var expired = await Backup(db).GetAsync(reserved.Request.Id, default);
        Check(expired.Request.Status == BackupRequestStatus.unreserved && expired.Reservation == null, "expiry persists after rejected pickup");
        Check((await EquipmentServiceFor(db).GetAsync(equipmentId, default)).Availability == EquipmentAvailability.available, "expired equipment becomes available");
    }
    BackupDetails next;
    await using (var db = Context()) next = await Backup(db).ReserveAsync(escalated.Request.Id, escalated.Request.RequestedBy, default);
    Check(next.Request.Status == BackupRequestStatus.reserved && next.Escalations.All(x => x.ResolvedAt != null), "retry after release reserves equipment and resolves escalation");
    await using (var db = Context())
    {
        await Reject(409, () => Backup(db).PickupAsync(reserved.Request.Id, allocationId, reserved.Request.RequestedBy, default), "stale reservation cannot pick up reassigned equipment");
    }
    await using (var db = Context())
    {
        var picked = await Backup(db).PickupAsync(next.Request.Id, next.Reservation!.Allocation.Id, next.Request.RequestedBy, default);
        Check(picked.Request.Status == BackupRequestStatus.in_use && picked.Reservation!.Allocation.PickedUpAt != null, "pickup marks request and allocation in_use");
        var duplicate = await Backup(db).PickupAsync(next.Request.Id, next.Reservation!.Allocation.Id, next.Request.RequestedBy, default);
        Check(duplicate.Reservation!.Allocation.PickedUpAt == picked.Reservation!.Allocation.PickedUpAt, "repeated pickup is idempotent");
    }
    clock.Advance(TimeSpan.FromHours(1));
    await using (var db = Context())
    {
        await Backup(db).ExpireAsync(default);
        Check((await EquipmentServiceFor(db).GetAsync(equipmentId, default)).Availability == EquipmentAvailability.in_use, "picked-up equipment never expires");
        await Reject(409, () => EquipmentServiceFor(db).UpdateAsync(equipmentId, new("Move", 1, 2, null, null, null, null, true, 1), default), "cannot move in-use equipment");
    }
    await using (var db = Context())
    {
        var returned = await Backup(db).ReturnAsync(next.Request.Id, next.Reservation!.Allocation.Id, next.Request.RequestedBy, default);
        Check(returned.Request.Status == BackupRequestStatus.returned && returned.Reservation == null, "return releases allocation");
        Check((await EquipmentServiceFor(db).GetAsync(equipmentId, default)).Availability == EquipmentAvailability.available, "return makes equipment available again");
    }
    var cancel = await Create();
    await using (var db = Context())
    {
        var cancelled = await Backup(db).CancelAsync(cancel.Request.Id, new(2, "No longer needed"), default);
        Check(cancelled.Request.Status == BackupRequestStatus.cancelled && cancelled.Reservation == null, "cancel releases reservation");
    }
    var sweep = await Create();
    clock.Advance(TimeSpan.FromMinutes(20));
    await using (var db = Context())
    {
        Check(await Backup(db).ExpireAsync(default) == 1, "background sweep releases reservation without client activity");
        Check(await Backup(db).ExpireAsync(default) == 0, "repeated expiry sweep is idempotent");
        await EquipmentServiceFor(db).DeleteAsync(equipmentId, default);
        Check((await EquipmentServiceFor(db).GetAsync(equipmentId, default)).Availability == EquipmentAvailability.inactive, "delete is a soft delete");
        var list = await Backup(db).ListAsync(new() { RequestedBy = 2, RequestedWard = WardType.ICU, BedNumber = "B-1", PageSize = 2 }, default);
        Check(list.TotalCount >= 2 && list.Items.Count == 2, "backup request list supports filters and pagination");
    }
    var shortage = await Create();
    Check(shortage.Request.Status == BackupRequestStatus.escalated, "ward stock and deleted equipment are not allocated");
    await using (var db = Context())
    {
        await Reject(400, () => Backup(db).CreateAsync(new(WardType.ICU, 2, 1, ""), default), "bed number required");
    }
    await using (var db = Context())
    {
        await Reject(400, () => Backup(db).CreateAsync(new(WardType.ICU, 999, 1, "B"), default), "unknown requester rejected");
    }
    await using (var db = Context())
    {
        await db.Users.Where(x => x.Id == 1).ExecuteUpdateAsync(x => x.SetProperty(u => u.IsActive, false));
    }
    var noAdmin = await Create();
    Check(noAdmin.Escalations.Single().AssignedAdminId == null, "shortage is recorded even when no admin is available");
    long fallbackId, preferredId;
    await using (var db = Context())
    {
        var service = EquipmentServiceFor(db);
        fallbackId = (await service.CreateAsync(new("FALLBACK", "Fallback ventilator", 1, 1, null, null, null, null, 2), default)).Equipment.Id;
        preferredId = (await service.CreateAsync(new("PREFERRED", "Preferred ventilator", 1, 1, null, null, null, null, 2), default)).Equipment.Id;
    }
    async Task<BackupDetails> RequestPreferred(long user)
    {
        await using var db = Context();
        return await Backup(db).CreateAsync(new(WardType.ICU, user, 1, "PREF-BED", preferredId), default);
    }
    var preferredRequests = await Task.WhenAll(RequestPreferred(2), RequestPreferred(3));
    Check(preferredRequests.All(x => x.Request.Status == BackupRequestStatus.reserved),
        "two concurrent requests for the same preferred equipment both reserve when alternatives exist");
    Check(preferredRequests.Select(x => x.Reservation!.Allocation.EquipmentId).Order().SequenceEqual(new[] { fallbackId, preferredId }.Order()),
        "concurrent preferred requests receive distinct matching warehouse equipment");
    Check(preferredRequests.All(x => x.Escalations.Count == 0), "no escalation while matching warehouse stock exists");
    var exhausted = await RequestPreferred(2);
    Check(exhausted.Request.Status == BackupRequestStatus.escalated, "preferred request escalates only after all matching stock is occupied");
    var fallbackRequest = preferredRequests.Single(x => x.Reservation!.Allocation.EquipmentId == fallbackId);
    var preferredRequest = preferredRequests.Single(x => x.Reservation!.Allocation.EquipmentId == preferredId);
    await using (var db = Context())
        await Backup(db).CancelAsync(fallbackRequest.Request.Id, new(fallbackRequest.Request.RequestedBy, "Release fallback"), default);
    await using (var db = Context())
        await Backup(db).PickupAsync(preferredRequest.Request.Id, preferredRequest.Reservation!.Allocation.Id, preferredRequest.Request.RequestedBy, default);
    await using (var db = Context())
    {
        var retry = await Backup(db).ReserveAsync(exhausted.Request.Id, 2, default);
        Check(retry.Reservation!.Allocation.EquipmentId == fallbackId && retry.Escalations.All(x => x.ResolvedAt != null),
            "retry selects released alternative when preferred equipment is in use and resolves escalation");
        await Backup(db).CancelAsync(retry.Request.Id, new(2, "Release fallback"), default);
    }
    await using (var db = Context())
        await Backup(db).ReturnAsync(preferredRequest.Request.Id, preferredRequest.Reservation!.Allocation.Id, preferredRequest.Request.RequestedBy, default);
    var preferredAvailable = await RequestPreferred(2);
    Check(preferredAvailable.Reservation!.Allocation.EquipmentId == preferredId,
        "available preferred equipment takes priority over lower-ID alternatives");
    Console.WriteLine($"All {checks} PostgreSQL integration checks passed.");
}
finally
{
    // Only remove the randomly generated schema created by this test invocation.
    await using var cleanup = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
    await cleanup.ExecuteNonQueryAsync();
}

sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan elapsed) => now += elapsed;
}
