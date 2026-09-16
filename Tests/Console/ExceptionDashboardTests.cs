using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CriticalCare.ConsoleApp;

internal static class ExceptionDashboardTests
{
    public static async Task RunAsync(
        Database database,
        IAuthenticationService authentication,
        Session admin,
        Session staff,
        Action<bool, string> check)
    {
        var service = new ExceptionDashboardService(database, authentication);
        var before = await service.GetAsync(admin);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string equipmentName;
        await using (var context = database.Open())
        {
            var equipment = await context.Equipment.FirstAsync();
            equipmentName = equipment.Name;
            var cases = new[]
            {
                (Number: "EX-OVERDUE", Due: today.AddDays(-1), Status: TicketStatus.open),
                (Number: "EX-TODAY", Due: today, Status: TicketStatus.open),
                (Number: "EX-CLOSED", Due: today.AddDays(-1), Status: TicketStatus.completed),
                (Number: "EX-CANCELLED", Due: today.AddDays(-1), Status: TicketStatus.cancelled)
            };
            foreach (var item in cases)
            {
                context.Tickets.Add(new Ticket
                {
                    TicketNumber = item.Number,
                    EquipmentId = equipment.Id,
                    CreatedById = admin.UserId,
                    TicketType = TicketType.preventive,
                    Status = item.Status,
                    DueDate = item.Due,
                    Title = "Exception dashboard verification",
                    Priority = TicketPriority.high
                });
            }

            var request = new BackupRequest
            {
                RequestNumber = "EX-BACKUP",
                RequestedBy = staff.UserId,
                EquipmentTypeId = equipment.EquipmentTypeId,
                RequestedWard = WardType.ICU,
                BedNumber = "EX-1",
                Status = BackupRequestStatus.escalated,
                Priority = BackupRequestPriority.medium
            };
            context.BackupRequests.Add(request);
            await context.SaveChangesAsync();
            context.BackupEscalations.Add(new BackupEscalation
            {
                RequestId = request.Id,
                AssignedAdminId = admin.UserId,
                Reason = "No available equipment"
            });
            context.BackupEscalations.Add(new BackupEscalation
            {
                RequestId = request.Id,
                AssignedAdminId = admin.UserId,
                Reason = "Previously resolved exception",
                ResolvedAt = DateTime.UtcNow,
                ResolvedBy = admin.UserId
            });
            await context.SaveChangesAsync();
        }

        var report = await service.GetAsync(admin);
        check(report.OverdueMaintenance.Count == before.OverdueMaintenance.Count + 1 &&
            report.OverdueMaintenance.Any(row => row.TicketId == "EX-OVERDUE" &&
                row.EquipmentName == equipmentName && row.Status == "overdue" && row.Priority == "high"),
            "exception dashboard excludes due-today, completed and cancelled maintenance from overdue records");
        check(report.UnresolvedTickets.Count == before.UnresolvedTickets.Count + 2,
            "exception dashboard counts only unresolved tickets");
        check(report.BackupExceptions.Count == before.BackupExceptions.Count + 1 &&
            report.BackupExceptions.Any(row => row.TicketId == "EX-BACKUP" &&
                row.EquipmentName.Contains("no equipment allocated")),
            "exception dashboard excludes resolved backup escalations and handles unallocated equipment");

        var denied = false;
        try
        {
            await service.GetAsync(staff);
        }
        catch (UnauthorizedAccessException)
        {
            denied = true;
        }
        check(denied, "staff cannot access the admin exception dashboard");

        var originalInput = Console.In;
        var originalOutput = Console.Out;
        using var input = new StringReader("1\n2\n3\n0\n");
        using var output = new StringWriter();
        try
        {
            Console.SetIn(input);
            Console.SetOut(output);
            await new ExceptionDashboardController(service).RunAsync(admin);
        }
        finally
        {
            Console.SetIn(originalInput);
            Console.SetOut(originalOutput);
        }
        var text = output.ToString();
        check(text.Contains("EX-OVERDUE") && text.Contains("EX-TODAY") && text.Contains("EX-BACKUP") &&
            new[] { "Ticket ID", "Type", "Equipment name", "Status", "Priority" }.All(text.Contains),
            "all three exception dashboard options display the requested record columns");
    }
}
