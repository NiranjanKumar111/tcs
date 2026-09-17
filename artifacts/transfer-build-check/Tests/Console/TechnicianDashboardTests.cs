using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CriticalCare.ConsoleApp;

internal static class TechnicianDashboardTests
{
    public static async Task RunAsync(Database database, IAuthenticationService auth, Session admin, Action<bool, string> check)
    {
        var employees = new EmployeeService(database, auth);
        const string password = "Technician-Dashboard-482!";
        var technician = await employees.SaveEmployeeAsync(admin, null, "Dashboard technician", "dashboard-tech@test.local", "technician", true, password);
        var other = await employees.SaveEmployeeAsync(admin, null, "Other dashboard technician", "dashboard-other@test.local", "technician", true, password);
        var session = await auth.LoginAsync("dashboard-tech@test.local", password);
        var maintenance = new MaintenanceService(database, auth);
        var reports = new ReportsService(database, auth);
        var equipment = new EquipmentManagementService(database, auth);
        long type, location, approver;
        await using (var db = database.Open())
        {
            type = await db.EquipmentTypes.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
            location = await db.Locations.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
            approver = await db.Users.Where(x => x.IsActive && x.Role == "approver").Select(x => x.Id).FirstAsync();
        }
        var assigned = await equipment.SaveEquipmentAsync(admin, null, new SaveEquipmentInput("Dashboard visible equipment", type, location, MaintenanceFrequency.none, technician, approver));
        var hidden = await equipment.SaveEquipmentAsync(admin, null, new SaveEquipmentInput("Dashboard hidden equipment", type, location, MaintenanceFrequency.none, technician, approver));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ticket = await maintenance.CreateAsync(admin, assigned, TicketType.preventive, TicketPriority.medium, "Dashboard assigned work", "Inspect", today, technician, approver);
        var otherTicket = await maintenance.CreateAsync(admin, assigned, TicketType.corrective, TicketPriority.medium, "Other technician work", "Inspect", today, other, approver);
        await maintenance.CreateAsync(admin, hidden, TicketType.preventive, TicketPriority.medium, "Hidden work", "Inspect", today, other, approver);
        var page = await reports.EquipmentAsync(session, new EquipmentQuery { PageSize = 1 });
        check(page.TotalCount == 1 && page.Items.Single().Equipment.Id == assigned,
            "technician equipment scope applies before pagination and ignores equipment links without assigned tickets");
        check((await reports.TechnicianEquipmentAsync(session, assigned)).Equipment.Id == assigned,
            "technician can view equipment by ID for an assigned ticket");
        async Task Denied(Func<Task> action, string name)
        {
            var denied = false;
            try { await action(); }
            catch (UnauthorizedAccessException) { denied = true; }
            check(denied, name);
        }
        await Denied(() => reports.TechnicianEquipmentAsync(session, hidden), "technician cannot read unassigned equipment by ID");
        await Denied(() => maintenance.DetailAsync(session, otherTicket), "technician cannot read another technician's ticket on shared equipment");
        await Denied(() => equipment.DeleteEquipmentAsync(session, assigned), "technician equipment access is read-only");
        check((await maintenance.ListAsync(session)).Single().TicketId == ticket, "assigned ticket list contains only the technician's own tickets");
        var savedInput = Console.In;
        var savedOutput = Console.Out;
        using var input = new StringReader($"1\n1\n2\n{assigned}\n0\n2\n1\n2\n{ticket}\nInspection response\nSafety check\ny\nPassed inspection\nn\ny\n{today:yyyy-MM-dd}\n0\n0\n");
        using var output = new StringWriter();
        try
        {
            Console.SetIn(input);
            Console.SetOut(output);
            await new TechnicianController(auth, maintenance, reports).RunAsync(session);
        }
        finally
        {
            Console.SetIn(savedInput);
            Console.SetOut(savedOutput);
        }
        var detail = await maintenance.DetailAsync(session, ticket);
        check(detail.Ticket.Status == TicketStatus.pending_approval && detail.Ticket.WorkReport == "Inspection response" &&
            detail.Ticket.ApproverId == approver && detail.Ticket.VersionNumber == 4,
            "technician menu saves response and checklist then submits to assigned approver with automatic versions");
        check(output.ToString().Contains("View all assigned equipment") && !output.ToString().Contains("Dashboard hidden equipment") &&
            output.ToString().Contains("2 Update ticket") && !output.ToString().Contains("3 Put response") &&
            !output.ToString().Contains("Current version:") && !output.ToString().Contains("Action not completed"),
            "technician dashboard supports scoped equipment and assigned ticket workflows");
        var reviewer = new Session(approver);
        check((await maintenance.ListAsync(reviewer, pendingOnly: true)).Any(x => x.TicketId == ticket), "submitted technician ticket appears in the assigned approver's pending queue");
        async Task<string> ReviewMenu(string actions)
        {
            using var reviewInput = new StringReader(actions);
            using var reviewOutput = new StringWriter();
            try
            {
                Console.SetIn(reviewInput);
                Console.SetOut(reviewOutput);
                await new ApproverController(auth, maintenance).RunAsync(reviewer);
            }
            finally
            {
                Console.SetIn(savedInput);
                Console.SetOut(savedOutput);
            }
            return reviewOutput.ToString();
        }
        var rejectedScreen = await ReviewMenu($"1\n2\n{ticket}\n2\n\nRepeat safety inspection\n0\n");
        detail = await maintenance.DetailAsync(session, ticket);
        check(detail.Ticket.Status == TicketStatus.rejected && detail.Ticket.AssignedTo == technician &&
            detail.Reviews.Last().Notes == "Repeat safety inspection" &&
            rejectedScreen.Contains($"{ticket} | preventive") && rejectedScreen.Contains("Inspection response") &&
            rejectedScreen.Contains("A rejection reason is required.") && !rejectedScreen.Contains("Current version:"),
            "approver sees pending ticket ID and technician response; rejection requires reason and retains technician assignment");
        check(!(await maintenance.ListAsync(reviewer, true)).Any(x => x.TicketId == ticket) &&
            (await maintenance.ListAsync(session)).Any(x => x.TicketId == ticket && x.Status == TicketStatus.rejected),
            "rejected ticket leaves pending approvals and appears for the respective technician's rework");
        await maintenance.ProgressAsync(session, ticket, detail.Ticket.VersionNumber, "Repeated inspection successfully");
        detail = await maintenance.DetailAsync(session, ticket);
        await maintenance.SubmitAsync(session, ticket, detail.Ticket.VersionNumber, today);
        var acceptedScreen = await ReviewMenu($"2\n{ticket}\n1\n\n0\n");
        detail = await maintenance.DetailAsync(session, ticket);
        check(detail.Ticket.Status == TicketStatus.approved && acceptedScreen.Contains("Repeated inspection successfully") &&
            acceptedScreen.Contains("Ticket accepted."), "approver accepts resubmitted technician response using automatic version");
        // Use the other still-open fixture ticket to verify reassignment removes access.
        await maintenance.CloseAsync(reviewer, ticket, detail.Ticket.VersionNumber);
        // Completed tickets still retain their assigned technician, so move the fixture assignment
        // directly for this visibility-only assertion after completing the workflow.
        await using (var db = database.Open())
        {
            (await db.Tickets.FindAsync(ticket))!.AssignedTo = other;
            await db.SaveChangesAsync();
        }
        check((await reports.EquipmentAsync(session, new EquipmentQuery())).TotalCount == 0, "equipment visibility follows current ticket reassignment");
        await Denied(() => reports.TechnicianEquipmentAsync(session, assigned), "reassignment removes previous technician's equipment ID access");
    }
}
