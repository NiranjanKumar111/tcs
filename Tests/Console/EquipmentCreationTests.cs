using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CriticalCare.ConsoleApp;

internal static class EquipmentCreationTests
{
    public static async Task RunAsync(Database database, IAuthenticationService authentication,
        Session admin, Session staff, Action<bool, string> check)
    {
        var service = new EquipmentManagementService(database, authentication);
        var employees = new EmployeeService(database, authentication);
        var scheduler = new SchedulingService(database, authentication);
        var reports = new ReportsService(database, authentication);
        var technician = await employees.SaveEmployeeAsync(admin, null, "Equipment flow technician",
            "equipment-flow-tech@test.local", "technician", true, "Equipment-Test-482!");
        var replacement = await employees.SaveEmployeeAsync(admin, null, "Replacement equipment technician",
            "equipment-replacement@test.local", "technician", true, "Equipment-Test-482!");
        long approver;
        long type;
        long location;
        string approverName;
        await using (var db = database.Open())
        {
            var reviewer = await db.Users.FirstAsync(user => user.IsActive && user.Role == "approver");
            approver = reviewer.Id;
            approverName = reviewer.Name;
            type = await db.EquipmentTypes.Select(item => item.Id).FirstAsync();
            location = await db.Locations.Select(item => item.Id).FirstAsync();
            db.Equipment.Add(new Equipment { EquipmentCode = "EQ-900", Name = "Existing code fixture",
                EquipmentTypeId = type, LocationId = location });
            await db.SaveChangesAsync();
        }
        var input = new SaveEquipmentInput("Assigned equipment A", type, location, MaintenanceFrequency.weekly, technician, approver);
        var ids = await Task.WhenAll(service.SaveEquipmentAsync(admin, null, input),
            service.SaveEquipmentAsync(admin, null, input with { Name = "Assigned equipment B" }));
        var first = await service.GetEquipmentAsync(admin, ids[0].ToString());
        var second = await service.GetEquipmentAsync(admin, ids[1].ToString());
        check(new[] { first.Inventory.Equipment.EquipmentCode, second.Inventory.Equipment.EquipmentCode }
            .Order().SequenceEqual(new[] { "EQ-901", "EQ-902" }), "concurrent equipment creation generates unique sequential codes after existing codes");
        var item = first.Inventory.Equipment;
        var creationDate = DateOnly.FromDateTime(item.CreatedAt);
        check(item.MaintenanceAnchorDate == creationDate && item.CalibrationAnchorDate == creationDate &&
            item.NextMaintenanceDate == creationDate.AddDays(7) && item.NextCalibrationDate == creationDate.AddDays(7) &&
            item.MaintenanceFrequency == item.CalibrationFrequency,
            "equipment creation uses today's anchor and one frequency for both schedules");
        check(first.Technicians.Single().Id == technician && first.Approvers.Single().Id == approver,
            "equipment creation persists exactly one technician and one approver");

        async Task Reject(SaveEquipmentInput invalid, Session actor, string message)
        {
            int before;
            await using (var db = database.Open()) before = await db.Equipment.CountAsync();
            var rejected = false;
            try { await service.SaveEquipmentAsync(actor, null, invalid); }
            catch (Exception error) when (error is ArgumentException or UnauthorizedAccessException) { rejected = true; }
            await using var after = database.Open();
            check(rejected && await after.Equipment.CountAsync() == before, message);
        }
        await Reject(input with { TechnicianId = approver }, admin, "wrong-role technician is rejected without creating equipment");
        await Reject(input with { ApproverId = 0 }, admin, "missing approver is rejected without creating equipment");
        await Reject(input, staff, "staff cannot create managed equipment");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using (var db = database.Open())
        {
            var existing = await db.Equipment.FindAsync(ids[0]);
            existing!.MaintenanceAnchorDate = today.AddDays(-5);
            existing.CalibrationAnchorDate = today.AddDays(-5);
            await db.SaveChangesAsync();
        }
        await service.SaveEquipmentAsync(admin, ids[0], input with { TechnicianId = replacement });
        var edited = await service.GetEquipmentAsync(admin, item.EquipmentCode);
        check(edited.Inventory.Equipment.EquipmentCode == item.EquipmentCode &&
            edited.Inventory.Equipment.MaintenanceAnchorDate == today.AddDays(-5) &&
            edited.Technicians.Single().Id == replacement && edited.Approvers.Single().Id == approver,
            "equipment edit preserves code and anchor while replacing its active assignment");

        await using (var db = database.Open())
        {
            var firstItem = await db.Equipment.FindAsync(ids[0]);
            firstItem!.NextMaintenanceDate = today.AddDays(2);
            firstItem.NextCalibrationDate = today.AddDays(3);
            var secondItem = await db.Equipment.FindAsync(ids[1]);
            secondItem!.NextMaintenanceDate = today.AddDays(3);
            secondItem.NextCalibrationDate = today.AddDays(3);
            await db.SaveChangesAsync();
        }
        await scheduler.GenerateAsync(admin);
        await scheduler.GenerateAsync(admin);
        await using (var db = database.Open())
        {
            var tickets = await db.Tickets.Where(ticket => ids.Contains(ticket.EquipmentId)).ToListAsync();
            check(tickets.Count == 1 && tickets[0].EquipmentId == ids[0] && tickets[0].TicketType == TicketType.preventive &&
                tickets[0].DueDate == today.AddDays(2),
                "scheduler generates at two days before due, excludes three days away and avoids duplicates");
            check(tickets[0].AssignedTo != null && tickets[0].ApproverId == approver,
                "scheduled maintenance assigns a technician and preserves the equipment approver");
            (await db.Equipment.FindAsync(ids[0]))!.NextCalibrationDate = today.AddDays(2);
            await db.SaveChangesAsync();
        }
        await scheduler.GenerateAsync(admin);
        await using (var db = database.Open())
        {
            check(await db.Tickets.AnyAsync(ticket => ticket.EquipmentId == ids[0] && ticket.TicketType == TicketType.calibration &&
                ticket.AssignedTo != null && ticket.ApproverId == approver),
                "calibration also generates two days early with equipment assignments");
        }
        var daily = await service.SaveEquipmentAsync(admin, null, input with { Name = "Daily equipment", Frequency = MaintenanceFrequency.daily });
        await scheduler.GenerateAsync(admin);
        await using (var db = database.Open())
        {
            check(await db.Tickets.CountAsync(ticket => ticket.EquipmentId == daily && ticket.DueDate == today.AddDays(1)) == 2,
                "daily equipment becomes eligible immediately without waiting for its due date");
        }

        var originalInput = Console.In;
        var originalOutput = Console.Out;
        using (var selectionInput = new StringReader("Unknown employee\n  SAME NAME  \n33\nSame Name\n22\n"))
        using (var selectionOutput = new StringWriter())
        {
            long selected;
            try
            {
                Console.SetIn(selectionInput);
                Console.SetOut(selectionOutput);
                selected = EquipmentAssignmentView.ReadEmployee("Technician", new[]
                {
                    new User { Id = 11, Name = "Same Name", Email = "first@test.local" },
                    new User { Id = 22, Name = "Same Name", Email = "second@test.local" },
                    new User { Id = 33, Name = "Different Name", Email = "third@test.local" }
                });
            }
            finally
            {
                Console.SetIn(originalInput);
                Console.SetOut(originalOutput);
            }
            check(selected == 22 && selectionOutput.ToString().Contains("The ID must belong") &&
                selectionOutput.ToString().Contains("Enter the full name"),
                "employee name selection handles case, whitespace, unknown names and duplicate-name ID validation");
        }
        using var consoleInput = new StringReader($"1\nName-selected equipment\n{type}\n{location}\nweekly\nEquipment flow technician\n{approverName}\n\n\n\n4\n5\n{item.EquipmentCode}\n0\n");
        using var output = new StringWriter();
        try
        {
            Console.SetIn(consoleInput);
            Console.SetOut(output);
            await new EquipmentController(authentication, service, reports).RunAsync(admin);
        }
        finally
        {
            Console.SetIn(originalInput);
            Console.SetOut(originalOutput);
        }
        var text = output.ToString();
        check(text.Contains("Equipment saved.") && text.Contains("4 View all equipment") && text.Contains("5 View specific equipment") &&
            !text.Contains("Unique equipment code:") && !text.Contains("Initial schedule anchor date YYYY-MM-DD:") &&
            !text.Contains("Calibration frequency ["), "equipment UI supports viewing and no longer prompts for code, anchor or a second frequency");
        await using (var db = database.Open())
        {
            var created = await db.Equipment.SingleAsync(equipment => equipment.Name == "Name-selected equipment");
            check(await db.EquipmentTechnicians.AnyAsync(link => link.EquipmentId == created.Id && link.TechnicianId == technician && link.IsActive) &&
                await db.EquipmentApprovers.AnyAsync(link => link.EquipmentId == created.Id && link.ApproverId == approver && link.IsActive),
                "entering existing employee names associates the selected employees with new equipment");
        }
    }
}
