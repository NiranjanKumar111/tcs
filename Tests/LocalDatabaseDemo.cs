using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

internal static class LocalDatabaseDemo
{
    // Explicit opt-in: inserts labeled demo data into the application's configured database.
    public static async Task RunAsync()
    {
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json").Build();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(config.GetConnectionString("DefaultConnection")).Options;
        AppDbContext Context() => new(options);
        BackupReservationService Backup(AppDbContext db) => new(db, TimeProvider.System);
        EquipmentService EquipmentServiceFor(AppDbContext db) => new(db, Backup(db), TimeProvider.System);
        var run = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
        var checks = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new Exception("FAILED: " + label);
            Console.WriteLine("PASS: " + label);
            checks++;
        }
        long typeId, warehouseId, firstUser, secondUser, firstEquipment, secondEquipment;
        await using (var db = Context())
        {
            warehouseId = await db.Locations.Where(x => x.IsActive && x.IsCentralWarehouse)
                .OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
            var type = new EquipmentType { TypeCode = "DEMO-" + run, TypeName = "Demo reservation test ventilator " + run };
            var staffA = new User { Name = "Demo Staff A " + run, Email = "demo-a-" + run + "@example.invalid", Role = "user" };
            var staffB = new User { Name = "Demo Staff B " + run, Email = "demo-b-" + run + "@example.invalid", Role = "user" };
            db.AddRange(type, staffA, staffB);
            await db.SaveChangesAsync();
            typeId = type.Id; firstUser = staffA.Id; secondUser = staffB.Id;
            var equipment = EquipmentServiceFor(db);
            firstEquipment = (await equipment.CreateAsync(new("DEMO-A-" + run, "Demo Backup Ventilator A", typeId, warehouseId,
                "DEMO-SN-A-" + run, "Demo", "Demo model", null, firstUser), default)).Equipment.Id;
            secondEquipment = (await equipment.CreateAsync(new("DEMO-B-" + run, "Demo Backup Ventilator B", typeId, warehouseId,
                "DEMO-SN-B-" + run, "Demo", "Demo model", null, secondUser), default)).Equipment.Id;
        }
        Console.WriteLine($"Inserted demo equipment IDs {firstEquipment}, {secondEquipment}; type ID {typeId}; warehouse ID {warehouseId}; staff IDs {firstUser}, {secondUser}.");
        async Task<BackupDetails> Request(long user, string bed)
        {
            await using var db = Context();
            return await Backup(db).CreateAsync(new(WardType.ICU, user, typeId, bed, firstEquipment,
                Reason: "Local PostgreSQL verification " + run), default);
        }
        var requests = await Task.WhenAll(Request(firstUser, "DEMO-BED-A"), Request(secondUser, "DEMO-BED-B"));
        Check(requests.All(x => x.Request.Status == BackupRequestStatus.reserved), "both simultaneous staff requests were reserved");
        Check(requests.Select(x => x.Reservation!.Allocation.EquipmentId).Distinct().Count() == 2,
            "same preferred item fell back to a different warehouse item; no double allocation");
        Check(requests.All(x => x.Escalations.Count == 0), "no escalation while alternative stock existed");
        foreach (var request in requests)
            Console.WriteLine($"Request {request.Request.Id}, staff {request.Request.RequestedBy} -> equipment {request.Reservation!.Allocation.EquipmentId}, allocation {request.Reservation.Allocation.Id}.");
        var shortage = await Request(firstUser, "DEMO-BED-C");
        Check(shortage.Request.Status == BackupRequestStatus.escalated && shortage.Escalations.Count == 1,
            "third request escalated only after both matching items were occupied");
        var first = requests[0];
        var second = requests[1];
        await using (var db = Context())
        {
            var picked = await Backup(db).PickupAsync(first.Request.Id, first.Reservation!.Allocation.Id, first.Request.RequestedBy, default);
            Check(picked.Request.Status == BackupRequestStatus.in_use, "pickup changed status to in_use");
            var occupied = await EquipmentServiceFor(db).GetAsync(first.Reservation.Allocation.EquipmentId, default);
            Check(occupied.Availability == EquipmentAvailability.in_use, "database reports picked-up equipment in use");
            await Backup(db).ReturnAsync(first.Request.Id, first.Reservation.Allocation.Id, first.Request.RequestedBy, default);
            var retry = await Backup(db).ReserveAsync(shortage.Request.Id, firstUser, default);
            Check(retry.Reservation?.Allocation.EquipmentId == first.Reservation.Allocation.EquipmentId,
                "returned item was allocated to waiting request");
            Check(retry.Escalations.All(x => x.ResolvedAt.HasValue), "successful retry resolved shortage escalation");
            await Backup(db).CancelAsync(shortage.Request.Id, new(firstUser, "Demo verification complete"), default);
        }
        // Advance only this demo reservation's persisted timestamps, not the app clock.
        await using (var db = Context())
        {
            var expiry = DateTime.UtcNow.AddMinutes(-1);
            await db.BackupAllocations.Where(x => x.Id == second.Reservation!.Allocation.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.AllocatedAt, expiry.AddMinutes(-20))
                    .SetProperty(a => a.ReservationExpiresAt, expiry));
            var expired = await Backup(db).GetAsync(second.Request.Id, default);
            Check(expired.Request.Status == BackupRequestStatus.unreserved && expired.Reservation == null,
                "past deadline released the reservation and marked request unreserved");
            await Backup(db).CancelAsync(second.Request.Id, new(second.Request.RequestedBy, "Demo expiry verification complete"), default);
            var items = await EquipmentServiceFor(db).ListAsync(new EquipmentQuery { EquipmentTypeId = typeId }, default);
            Check(items.TotalCount == 2 && items.Items.All(x => x.Availability == EquipmentAvailability.available),
                "both demo equipment items are available after verification");
        }
        Console.WriteLine($"All {checks} local database checks passed. Demo data and request history retained. Filter GET /api/equipment?equipmentTypeId={typeId}");
    }
}
