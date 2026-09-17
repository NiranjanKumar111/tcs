using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CriticalCare.ConsoleApp;

internal static class SelfTests
{
    public static async Task RunAsync(string connection)
    {
        var schema = "console_test_" + Guid.NewGuid().ToString("N");
        await using var owner = new NpgsqlConnection(connection);
        await owner.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", owner))
        {
            await create.ExecuteNonQueryAsync();
        }

        var isolated = new NpgsqlConnectionStringBuilder(connection)
        {
            SearchPath = schema
        };
        var database = new Database(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(isolated.ConnectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options);
        var checks = 0;
        void Check(bool value, string name)
        {
            if (!value)
            {
                throw new Exception("FAILED: " + name);
            }

            checks++;
            Console.WriteLine("PASS: " + name);
        }

        async Task Reject(Func<Task> action, string name)
        {
            try
            {
                await action();
            }
            catch (Exception e)when (e is UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                Check(true, name);
                return;
            }

            throw new Exception("FAILED rejection: " + name);
        }

        try
        {
            await using (var db = database.Open())
            {
                await db.Database.MigrateAsync();
            }

            var auth = new AuthenticationService(database);
            var employees = new EmployeeService(database, auth);
            var locations = new LocationService(database, auth);
            var equipmentTypes = new EquipmentTypeService(database, auth);
            var equipmentManagement = new EquipmentManagementService(database, auth);
            var maintenance = new MaintenanceService(database, auth);
            var backups = new BackupsService(database, auth);
            var reports = new ReportsService(database, auth);
            const string password = "Test-Only-Password-482!";
            await auth.BootstrapAsync("Test Admin", "admin@console.test", password);
            Check(!await auth.NeedsBootstrapAsync(), "bootstrap cannot repeat");
            await Reject(() => auth.BootstrapAsync("Another", "another@console.test", password), "second bootstrap rejected");
            await Reject(() => auth.LoginAsync("admin@console.test", "incorrect"), "incorrect login rejected");
            var admin = await auth.LoginAsync("admin@console.test", password);
            var techId = await employees.SaveEmployeeAsync(
                admin,
                null,
                "Technician",
                "tech@console.test",
                "technician",
                true,
                password);
            var approverId = await employees.SaveEmployeeAsync(
                admin,
                null,
                "Approver",
                "approve@console.test",
                "approver",
                true,
                password);
            var staffId = await employees.SaveEmployeeAsync(
                admin,
                null,
                "Staff",
                "staff@console.test",
                "staff",
                true,
                password);
            await employees.SaveEmployeeAsync(
                admin,
                null,
                "Staff B",
                "staffb@console.test",
                "staff",
                true,
                password);
            var tech = await auth.LoginAsync("tech@console.test", password);
            var approver = await auth.LoginAsync("approve@console.test", password);
            var staff = await auth.LoginAsync("staff@console.test", password);
            var staffB = await auth.LoginAsync("staffb@console.test", password);
            await employees.SaveEmployeeAsync(
                admin,
                null,
                "Approver B",
                "approveb@console.test",
                "approver",
                true,
                password);
            var otherApprover = await auth.LoginAsync("approveb@console.test", password);
            await Reject(() => employees.SaveEmployeeAsync(
                    staff,
                    null,
                    "Hack",
                    "hack@console.test",
                    "admin",
                    true,
                    password), "staff cannot create employees");
            await Reject(() => employees.SaveEmployeeAsync(
                    admin,
                    admin.UserId,
                    "Admin",
                    "admin@console.test",
                    "staff",
                    true,
                    null), "admin cannot demote self");
            var location = await locations.SaveLocationAsync(
                admin,
                null,
                "TEST-WH",
                "Warehouse",
                "Block A",
                "Ground",
                WardType.OTHER,
                "Store",
                "S1",
                true);
            var type = await equipmentTypes.AddTypeAsync(admin, "TEST-VENT", "Test ventilator");
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var equipment = await equipmentManagement.SaveEquipmentAsync(admin, null,
                new EquipmentManagementBackend.DTOs.SaveEquipmentInput("Vent A", type, location, MaintenanceFrequency.monthly, techId, approverId));
            var secondEquipment = await equipmentManagement.SaveEquipmentAsync(admin, null,
                new EquipmentManagementBackend.DTOs.SaveEquipmentInput("Vent B", type, location, MaintenanceFrequency.monthly, techId, approverId));
            await using (var db = database.Open())
            {
                var row = await db.Equipment.FindAsync(equipment);
                Check(row!.NextMaintenanceDate == today.AddMonths(1), "initial next maintenance date calculated");
                Check((await db.Locations.FindAsync(location))!.Building == "Block A", "detailed location persisted");
                Check((await db.Users.FindAsync(staffId))!.PasswordHash != password, "password stored as hash");
            }

            Check(Schedule.Next(new DateOnly(2024, 1, 31), MaintenanceFrequency.monthly) == new DateOnly(2024, 2, 29), "month-end leap-year calculation");
            Check(Schedule.Next(new DateOnly(2024, 2, 29), MaintenanceFrequency.yearly) == new DateOnly(2025, 2, 28), "yearly leap-day calculation");
            foreach (var frequency in Enum.GetValues<MaintenanceFrequency>())
            {
                Check(frequency == MaintenanceFrequency.none ? Schedule.Next(today, frequency) == null : Schedule.Next(today, frequency) > today, "schedule interval " + frequency);
            }

            var ticket = await maintenance.CreateAsync(
                admin,
                equipment,
                TicketType.preventive,
                TicketPriority.high,
                "Preventive test",
                "Inspect",
                today,
                techId,
                approverId);
            async Task<int> Version(long id)
            {
                await using var db = database.Open();
                return (await db.Tickets.FindAsync(id))!.VersionNumber;
            }

            await Reject(() => maintenance.ProgressAsync(
                    staff,
                    ticket,
                    1,
                    "Unauthorized"), "staff cannot update maintenance work");
            await Reject(() => maintenance.CloseAsync(approver, ticket, 1), "cannot close before approval");
            await maintenance.ProgressAsync(
                tech,
                ticket,
                await Version(ticket),
                "Inspection performed");
            await Reject(() => maintenance.ProgressAsync(
                    tech,
                    ticket,
                    1,
                    "Stale"), "stale version rejected");
            await Reject(async () => await maintenance.SubmitAsync(
                    tech,
                    ticket,
                    await Version(ticket),
                    today), "cannot submit without checklist evidence");
            await maintenance.ChecklistAsync(
                tech,
                ticket,
                await Version(ticket),
                "Safety checks",
                true,
                "Passed");
            await maintenance.SubmitAsync(
                tech,
                ticket,
                await Version(ticket),
                today);
            await Reject(async () => await maintenance.ReviewAsync(
                    otherApprover,
                    ticket,
                    await Version(ticket),
                    true,
                    "Wrong reviewer"), "unassigned approver cannot review");
            await Reject(async () => await maintenance.ChecklistAsync(
                    tech,
                    ticket,
                    await Version(ticket),
                    "Change evidence",
                    true,
                    "Changed"), "submitted evidence cannot be edited");
            await Reject(async () => await maintenance.ReviewAsync(
                    tech,
                    ticket,
                    await Version(ticket),
                    true,
                    "Self approval"), "technician cannot approve own work");
            await maintenance.ReviewAsync(
                approver,
                ticket,
                await Version(ticket),
                false,
                "Please include extra evidence");
            await maintenance.ProgressAsync(
                tech,
                ticket,
                await Version(ticket),
                "Rework and extra evidence recorded");
            await maintenance.SubmitAsync(
                tech,
                ticket,
                await Version(ticket),
                today);
            await maintenance.ReviewAsync(
                approver,
                ticket,
                await Version(ticket),
                true,
                "Evidence verified");
            await maintenance.CloseAsync(approver, ticket, await Version(ticket));
            await using (var db = database.Open())
            {
                var item = await db.Equipment.FindAsync(equipment);
                Check(item!.LastMaintenanceDate == today && item.NextMaintenanceDate == today.AddMonths(1), "approved closure advances maintenance schedule");
                Check(await db.TicketComplianceVerifications.CountAsync(x => x.TicketId == ticket) == 2, "reject and approve history retained");
                Check(await db.AuditLogs.AnyAsync(x => x.EntityType == "Ticket" && x.EntityId == ticket.ToString()), "workflow audit records persisted");
            }

            var cal = await maintenance.CreateAsync(
                admin,
                equipment,
                TicketType.calibration,
                TicketPriority.medium,
                "Calibration",
                "Measure",
                today,
                techId,
                approverId);
            await maintenance.ProgressAsync(
                tech,
                cal,
                await Version(cal),
                "Measured output");
            await maintenance.ChecklistAsync(
                tech,
                cal,
                await Version(cal),
                "Calibration procedure",
                true,
                "Done");
            await maintenance.CalibrationAsync(
                tech,
                cal,
                await Version(cal),
                "Output",
                "unit",
                9,
                11,
                12);
            await Reject(async () => await maintenance.SubmitAsync(
                    tech,
                    cal,
                    await Version(cal),
                    today), "out-of-range calibration cannot be submitted");
            await maintenance.CalibrationAsync(
                tech,
                cal,
                await Version(cal),
                "Output",
                "unit",
                9,
                11,
                10);
            await maintenance.SubmitAsync(
                tech,
                cal,
                await Version(cal),
                today);
            await maintenance.ReviewAsync(
                approver,
                cal,
                await Version(cal),
                true,
                "Readings checked");
            await maintenance.CloseAsync(approver, cal, await Version(cal));
            await using (var db = database.Open())
            {
                Check((await db.Equipment.FindAsync(equipment))!.NextCalibrationDate == today.AddMonths(1), "calibration closure advances only its schedule");
            }

            var ticketDetails = await maintenance.DetailAsync(approver, ticket);
            Check(ticketDetails.Ticket.Status == TicketStatus.completed &&
                ticketDetails.Checklist.Count > 0 && ticketDetails.Reviews.Count == 2 && ticketDetails.History.Count > 0,
                "ticket detail data retains evidence and review history");
            var dashboard = await reports.DashboardAsync(admin);
            Check(dashboard.ActiveEquipmentCount == 2 && dashboard.ConfiguredMaintenanceCount == 2 &&
                dashboard.VerifiedMaintenanceCount == 1 && dashboard.VerifiedCalibrationCount == 1,
                "dashboard data preserves verified compliance counts");
            var referenceData = await reports.ReferencesAsync(staff);
            Check(referenceData.EquipmentTypes.Any(item => item.Id == type) &&
                referenceData.Locations.Any(item => item.Id == location), "reference data includes types and locations");
            var equipmentPage = await reports.EquipmentAsync(staff, new EquipmentManagementBackend.DTOs.EquipmentQuery());
            Check(equipmentPage.TotalCount == 2 && equipmentPage.Items.Count == 2,
                "equipment report preserves pagination and rows");
            await BackupFlowTests.RunAsync(staff, type, auth, backups, reports, Check);
            var simultaneous = await Task.WhenAll(backups.RequestAsync(
                    staff,
                    type,
                    WardType.ICU,
                    "A1",
                    equipment), backups.RequestAsync(
                    staffB,
                    type,
                    WardType.ICU,
                    "A2",
                    equipment));
            Check(simultaneous.All(x => x.Reservation != null) && simultaneous.Select(x => x.Reservation!.Equipment.Id).Distinct().Count() == 2, "concurrent staff requests select different warehouse equipment");
            await Reject(() => backups.DetailsAsync(staffB, simultaneous[0].Request.Id), "staff cannot view another staff allocation history");
            var ownAllocations = await backups.ListAsync(staff);
            Check(ownAllocations.Items.Count == 3 && ownAllocations.Items.All(request => request.RequestedBy == staff.UserId),
                "backup list data remains isolated to the requesting staff member");
            var shortage = await backups.RequestAsync(
                staff,
                type,
                WardType.ICU,
                "A3",
                equipment);
            Check(shortage.Request.Status == BackupRequestStatus.escalated, "escalate only after matching stock exhausted");
            var first = simultaneous[0];
            await backups.ActionAsync(
                staff,
                "pickup",
                first.Request.Id,
                first.Reservation!.Allocation.Id);
            await backups.ActionAsync(
                staff,
                "return",
                first.Request.Id,
                first.Reservation.Allocation.Id);
            var retry = await backups.ActionAsync(staff, "reserve", shortage.Request.Id);
            Check(retry.Reservation != null && retry.Escalations.All(x => x.ResolvedAt != null), "return and retry resolve escalation");
            await backups.ActionAsync(staff, "cancel", retry.Request.Id);
            await using (var db = database.Open())
            {
                await db.BackupAllocations.Where(x => x.Id == simultaneous[1].Reservation!.Allocation.Id).ExecuteUpdateAsync(x => x.SetProperty(a => a.ReservationExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
            }

            Check((await backups.DetailsAsync(staffB, simultaneous[1].Request.Id)).Request.Status == BackupRequestStatus.unreserved, "expired reservation becomes unreserved");
            var issue = await maintenance.CreateAsync(
                staff,
                equipment,
                TicketType.corrective,
                TicketPriority.critical,
                "Fault report",
                "Issue",
                today);
            var fallback = await backups.RequestAsync(
                staff,
                type,
                WardType.ICU,
                "B1",
                equipment);
            Check(fallback.Reservation!.Equipment.Id == secondEquipment, "equipment with open maintenance is not allocated");
            Check((await reports.DashboardAsync(admin, true)).TicketExceptions.Any(ticket => ticket.Title == "Fault report"), "exception dashboard includes critical issue");
            await Reject(() => reports.DashboardAsync(staff), "staff cannot access admin compliance dashboard");
            await employees.SaveEmployeeAsync(
                admin,
                staffB.UserId,
                "Staff B",
                "staffb@console.test",
                "staff",
                false,
                null);
            await Reject(() => auth.CurrentAsync(staffB), "disabled session rejected on next action");
            await ExceptionDashboardTests.RunAsync(database, auth, admin, staff, Check);
            await AdminMenuTests.RunAsync(database, auth, admin, staff, Check);
            await EquipmentCreationTests.RunAsync(database, auth, admin, staff, Check);
            await TechnicianDashboardTests.RunAsync(database, auth, admin, Check);
            foreach (var actor in new[] { admin, staff })
            {
                foreach (var kind in Enum.GetValues<TicketType>())
                {
                    var id = await maintenance.CreateAsync(actor, equipment, kind);
                    var created = (await maintenance.DetailAsync(actor, id)).Ticket;
                    Check(created.Priority == (kind == TicketType.calibration ? TicketPriority.low : TicketPriority.medium) &&
                        created.DueDate == DateOnly.FromDateTime(created.CreatedAt).AddDays(3),
                        $"automatic {kind} priority and registration-plus-three-day due date for user {actor.UserId}");
                }
                var savedInput = Console.In;
                var savedOutput = Console.Out;
                using var menuInput = new StringReader($"1\n{equipment}\ncalibration\n2\n0\n");
                using var menuOutput = new StringWriter();
                try
                {
                    Console.SetIn(menuInput);
                    Console.SetOut(menuOutput);
                    if (actor == admin) await new AdminTicketController(auth, maintenance, employees).RunAsync(actor);
                    else await new IncidentController(auth, maintenance, reports).RunAsync(actor);
                }
                finally
                {
                    Console.SetIn(savedInput);
                    Console.SetOut(savedOutput);
                }
                var screen = menuOutput.ToString();
                Check(screen.Contains("Created ticket") && screen.Contains("2 View ticket history") &&
                    !screen.Contains("Title:") && !screen.Contains("Description:") && !screen.Contains("Due date:") &&
                    !screen.Contains("Priority [") && !screen.Contains("Action not completed"),
                    "simplified ticket menu creates and displays history using only equipment ID and ticket type");
            }
            await PasswordChangeTests.RunAsync(database, auth, admin, Check);
            Console.WriteLine($"ALL {checks} CONSOLE POSTGRESQL CHECKS PASSED");
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", owner);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
