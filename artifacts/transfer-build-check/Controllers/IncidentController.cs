using CriticalCare.ConsoleApp.Views.Staff;
using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.Views.Shared.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class IncidentController : MenuController
{
    private readonly IMaintenanceService maintenance;
    public IncidentController(IAuthenticationService auth, IMaintenanceService maintenance, IReportsService reports) : base(auth)
    {
        this.maintenance = maintenance;
    }

    public Task RunAsync(Session session) => RunMenuAsync(session, "staff", "Ticket", new()
    {
        ["1"] = ("Create ticket", () => CreateAsync(session)),
        ["2"] = ("View ticket history", () => HistoryAsync(session))
    });

    private async Task CreateAsync(Session session)
    {
        var (equipment, type) = StaffTicketView.ReadRequest();
        var id = await maintenance.CreateAsync(session, equipment, type);
        StaffTicketView.ShowCreated(id, await maintenance.DetailAsync(session, id));
    }

    private async Task HistoryAsync(Session session)
    {
        var tickets = await maintenance.ListAsync(session);
        if (tickets.Count == 0) ConsoleView.ShowMessage("No tickets found.");
        foreach (var ticket in tickets)
            TicketView.ShowDetails(await maintenance.DetailAsync(session, ticket.TicketId));
    }
}
