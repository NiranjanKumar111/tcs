using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class AdminTicketController : MenuController
{
    private readonly IMaintenanceService maintenance;
    private readonly IEmployeeService employees;

    public AdminTicketController(IAuthenticationService authentication, IMaintenanceService maintenance,
        IEmployeeService employees) : base(authentication)
    {
        this.maintenance = maintenance;
        this.employees = employees;
    }

    public Task RunAsync(Session session) => RunMenuAsync(session, "admin", "Ticket", new()
    {
        ["1"] = ("Create ticket", () => CreateAsync(session)),
        ["2"] = ("Reassign ticket to new technician", () => ReassignAsync(session)),
        ["3"] = ("All ticket history / details", () => HistoryAsync(session))
    });

    private async Task CreateAsync(Session session)
    {
        var equipment = Number("Equipment ID");
        var type = Choice<TicketType>("Ticket type");
        var priority = Choice<TicketPriority>("Priority");
        var title = Read("Title");
        var description = Read("Description");
        var dueDate = Date("Due date");
        var technician = OptionalId("Technician ID (blank=automatic)");
        var approver = OptionalId("Approver ID (blank=automatic)");
        var id = await maintenance.CreateAsync(session, equipment, type, priority, title, description,
            dueDate, technician, approver);
        ConsoleView.ShowMessage($"Created ticket {id}.");
    }

    private async Task ReassignAsync(Session session)
    {
        TicketView.ShowTickets(await maintenance.ListAsync(session));
        var ticket = Number("Ticket ID");
        var details = await maintenance.DetailAsync(session, ticket);
        var technicians = (await employees.EmployeesAsync(session))
            .Where(employee => employee.IsActive && employee.Role == "technician")
            .OrderBy(employee => employee.Name).ThenBy(employee => employee.Id).ToList();
        if (technicians.Count == 0)
        {
            ConsoleView.ShowMessage("No active technicians available. Add a technician under Employee first.");
            return;
        }
        var technician = EquipmentAssignmentView.ReadEmployee("Technician", technicians);
        await maintenance.ReassignTechnicianAsync(session, ticket, details.Ticket.VersionNumber, technician);
        ConsoleView.ShowMessage("Technician reassigned. The approver is unchanged.");
    }

    private async Task HistoryAsync(Session session)
    {
        var tickets = await maintenance.ListAsync(session);
        if (tickets.Count == 0)
        {
            ConsoleView.ShowMessage("No tickets found.");
            return;
        }
        foreach (var ticket in tickets)
        {
            TicketView.ShowDetails(await maintenance.DetailAsync(session, ticket.TicketId));
        }
    }
}
