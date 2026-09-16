using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CriticalCare.ConsoleApp;

internal static class AdminMenuTests
{
    public static async Task RunAsync(Database database, IAuthenticationService authentication,
        Session admin, Session staff, Action<bool, string> check)
    {
        var employees = new EmployeeService(database, authentication);
        var equipment = new EquipmentManagementService(database, authentication);
        var maintenance = new MaintenanceService(database, authentication);
        var reports = new ReportsService(database, authentication);
        var backups = new BackupsService(database, authentication);
        var password = "Admin-Menu-Test-482!";
        var firstTechnician = await employees.SaveEmployeeAsync(admin, null, "Menu technician A",
            "menu-tech-a@test.local", "technician", true, password);
        var secondTechnician = await employees.SaveEmployeeAsync(admin, null, "Menu technician B",
            "menu-tech-b@test.local", "technician", true, password);
        await employees.SaveEmployeeAsync(admin, firstTechnician, "Updated technician",
            "menu-tech-a@test.local", "technician", true, null);

        long equipmentType;
        long location;
        long approver;
        await using (var context = database.Open())
        {
            equipmentType = await context.EquipmentTypes.Select(item => item.Id).FirstAsync();
            location = await context.Locations.Select(item => item.Id).FirstAsync();
            approver = await context.Users.Where(user => user.Role == "approver" && user.IsActive)
                .Select(user => user.Id).FirstAsync();
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var equipmentId = await equipment.SaveEquipmentAsync(admin, null,
            new EquipmentManagementBackend.DTOs.SaveEquipmentInput("Menu equipment", equipmentType, location,
                MaintenanceFrequency.none, firstTechnician, approver));
        await equipment.SaveEquipmentAsync(admin, equipmentId,
            new EquipmentManagementBackend.DTOs.SaveEquipmentInput("Updated menu equipment", equipmentType, location,
                MaintenanceFrequency.none, firstTechnician, approver));
        var ticketId = await maintenance.CreateAsync(admin, equipmentId, TicketType.corrective,
            TicketPriority.medium, "Menu ticket", "Menu test", today, firstTechnician, approver);
        var original = await maintenance.DetailAsync(admin, ticketId);
        await maintenance.ReassignTechnicianAsync(admin, ticketId, original.Ticket.VersionNumber, secondTechnician);
        var reassigned = await maintenance.DetailAsync(admin, ticketId);
        check(reassigned.Ticket.AssignedTo == secondTechnician && reassigned.Ticket.ApproverId == approver &&
            reassigned.History.Any(entry => entry.Action == TicketHistoryAction.reassigned),
            "admin technician reassignment preserves approver and records history");

        async Task Denied(Func<Task> action, string description)
        {
            var rejected = false;
            try { await action(); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            {
                rejected = true;
            }
            check(rejected, description);
        }
        await Denied(() => maintenance.ReassignTechnicianAsync(admin, ticketId, original.Ticket.VersionNumber, firstTechnician),
            "technician reassignment rejects stale versions");
        var savedInput = Console.In;
        var savedOutput = Console.Out;
        using (var reassignInput = new StringReader($"2\n{ticketId}\nUpdated technician\n0\n"))
        using (var reassignOutput = new StringWriter())
        {
            try
            {
                Console.SetIn(reassignInput);
                Console.SetOut(reassignOutput);
                await new AdminTicketController(authentication, maintenance, employees).RunAsync(admin);
            }
            finally
            {
                Console.SetIn(savedInput);
                Console.SetOut(savedOutput);
            }
            var updated = await maintenance.DetailAsync(admin, ticketId);
            var screen = reassignOutput.ToString();
            check(updated.Ticket.AssignedTo == firstTechnician && updated.Ticket.ApproverId == approver &&
                updated.Ticket.VersionNumber == reassigned.Ticket.VersionNumber + 1 &&
                screen.Contains("menu-tech-a@test.local") && screen.Contains("menu-tech-b@test.local") &&
                !screen.Contains("Current version:") && !screen.Contains("New technician ID:"),
                "reassignment selects a listed technician by name and automatically reads and advances the ticket version");
            await maintenance.ReassignTechnicianAsync(admin, ticketId, updated.Ticket.VersionNumber, secondTechnician);
        }
        await Denied(() => employees.DeleteEmployeeAsync(admin, admin.UserId), "admin cannot delete own account");
        await Denied(() => employees.DeleteEmployeeAsync(admin, secondTechnician), "employee with open tickets cannot be deleted");
        await Denied(() => employees.DeleteEmployeeAsync(staff, firstTechnician), "staff cannot delete employees");
        await employees.DeleteEmployeeAsync(admin, firstTechnician);
        await using (var context = database.Open())
        {
            check(!(await context.Users.FindAsync(firstTechnician))!.IsActive,
                "employee deletion disables login and preserves the employee record");
            // End the fixture ticket so equipment can be deleted through the normal service.
            (await context.Tickets.FindAsync(ticketId))!.Status = TicketStatus.cancelled;
            await context.SaveChangesAsync();
        }
        await equipment.DeleteEquipmentAsync(admin, equipmentId);
        await employees.EmployeesAsync(admin);
        var viewedEmployee = await employees.GetEmployeeAsync(admin, firstTechnician);
        check(viewedEmployee.Id == firstTechnician && !viewedEmployee.IsActive,
            "employee details include retained inactive accounts");
        await Denied(() => employees.GetEmployeeAsync(admin, long.MaxValue), "missing employee lookup is rejected");
        await Denied(() => employees.GetEmployeeAsync(staff, firstTechnician), "staff cannot view employee details");
        await Denied(() => employees.EmployeesAsync(staff), "staff cannot list employees");
        await reports.EquipmentAsync(admin, new EquipmentManagementBackend.DTOs.EquipmentQuery());

        await using (var context = database.Open())
        {
            for (var index = 0; index < 105; index++)
            {
                context.AuditLogs.Add(new AuditLog { UserId = admin.UserId, EntityType = "Test", Description = $"Audit page fixture {index}" });
            }
            await context.SaveChangesAsync();
        }
        var audit = await reports.AuditAsync(admin);
        await using (var context = database.Open())
        {
            check(audit.Count == await context.AuditLogs.CountAsync() && audit.Count > 100,
                "audit view receives all historical records beyond the old 100 record limit");
        }
        foreach (var entity in new[] { (Type: "User", Id: firstTechnician), (Type: "Equipment", Id: equipmentId) })
        {
            check(new[] { AuditAction.create, AuditAction.update, AuditAction.delete }.All(action =>
                audit.Any(row => row.EntityType == entity.Type && row.EntityId == entity.Id.ToString() && row.Action == action)),
                $"{entity.Type} create edit and delete actions are audited");
        }
        check(audit.Any(row => row.Action == AuditAction.login && row.Result == AuditResult.success) &&
            audit.Any(row => row.Action == AuditAction.login && row.Result == AuditResult.failure) &&
            audit.Any(row => row.EntityType == "Ticket" && row.Action == AuditAction.create) &&
            audit.Any(row => row.EntityType == "BackupAllocation" && row.Action == AuditAction.create),
            "audit history includes successful and failed login, ticket generation and backup allocation");

        var controller = new AdminController(authentication, reports,
            new EmployeeController(authentication, employees), new EquipmentController(authentication, equipment, reports),
            new AdminTicketController(authentication, maintenance, employees), new BackupController(authentication, backups, reports),
            new ExceptionDashboardController(new ExceptionDashboardService(database, authentication)));
        var originalInput = Console.In;
        var originalOutput = Console.Out;
        using var input = new StringReader($"2\n4\n5\n{firstTechnician}\n0\n3\n0\n4\n3\n0\n5\n2\n0\n6\n0\n7\n0\n");
        using var output = new StringWriter();
        try
        {
            Console.SetIn(input);
            Console.SetOut(output);
            await controller.RunAsync(admin);
        }
        finally
        {
            Console.SetIn(originalInput);
            Console.SetOut(originalOutput);
        }
        var text = output.ToString();
        check(text.Contains("4 View all employees") && text.Contains("5 View specific employee") &&
            text.Split("menu-tech-a@test.local").Length >= 3 &&
            !text.Contains(viewedEmployee.PasswordHash!),
            "employee list and specific view display employee records without exposing password hashes");
        check(audit.Any(row => row.EntityType == "User" && row.EntityId == firstTechnician.ToString() &&
            row.Action == AuditAction.view), "specific employee viewing is audited");
        check(new[] { "1 Dashboard KPI", "2 Employee", "3 Equipment", "4 Ticket", "5 Backup allocation",
            "6 Exception dashboard", "7 Audit logs", "1 Add", "2 Delete", "3 Edit", "1 Create ticket",
            "2 Reassign ticket to new technician", "3 All ticket history / details", "1 Request backup allocation",
            "2 Allocation history", "AUDIT HISTORY" }.All(text.Contains) &&
            !text.Contains("41 Request backup") && !text.Contains("10 Equipment KPIs"),
            "admin navigation contains only the requested numbered menus and submenus");
        check((await backups.ListAsync(admin, ownOnly: true)).Items.All(request => request.RequestedBy == admin.UserId),
            "admin personal backup history uses the same ownership filter as staff");
    }
}
