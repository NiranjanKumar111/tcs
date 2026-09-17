using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.Views.Shared.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class AdminTicketController : MenuController
{
    private readonly IMaintenanceService maintenance;
    public AdminTicketController(IAuthenticationService auth, IMaintenanceService maintenance, IEmployeeService employees) : base(auth)
    {
        this.maintenance = maintenance;
    }

    public Task RunAsync(Session session) => RunMenuAsync(session, "admin", "Ticket", new()
    {
        ["1"] = ("Create ticket", () => CreateAsync(session)),
        ["2"] = ("View ticket history", () => HistoryAsync(session))
    });

    private async Task CreateAsync(Session session)
    {
        var equipment = Number("Equipment ID");
        var type = Choice<TicketType>("Ticket type");
        var id = await maintenance.CreateAsync(session, equipment, type);
        ConsoleView.ShowMessage($"Created ticket {id}.");
        TicketView.ShowDetails(await maintenance.DetailAsync(session, id));
    }

    private async Task HistoryAsync(Session session)
    {
        var tickets = await maintenance.ListAsync(session);
        if (tickets.Count == 0) ConsoleView.ShowMessage("No tickets found.");
        foreach (var ticket in tickets)
            TicketView.ShowDetails(await maintenance.DetailAsync(session, ticket.TicketId));
    }
}
