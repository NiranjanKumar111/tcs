using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class IncidentController : MenuController
{
    private readonly IMaintenanceService maintenance;
    private readonly IReportsService reports;

    public IncidentController(IAuthenticationService auth, IMaintenanceService maintenance, IReportsService reports) : base(auth)
    {
        this.maintenance = maintenance;
        this.reports = reports;
    }

    public Task RunAsync(Session session) => RunMenuAsync(
        session,
        "staff",
        "Incident",
        new()
        {
            ["1"] = ("Equipment availability/filter", () => ShowEquipmentAsync(session)),
            ["2"] = ("Equipment types and locations", () => ShowReferencesAsync(session)),
            ["3"] = ("Raise faulty equipment incident", () => RaiseFaultyEquipmentIncidentAsync(session)),
            ["4"] = ("Ticket logs", () => TicketLogsAsync(session)),
            ["5"] = ("Ticket details/history", () => ShowTicketDetailsAsync(session)),

        });
    private async Task ShowEquipmentAsync(Session session)
    {
        EquipmentListView.Show(await reports.EquipmentAsync(session, new EquipmentQuery {
                    Search = Optional("Search (blank=all)"),
                    EquipmentTypeId = OptionalId("Type ID (blank=all)"),
                    LocationId = OptionalId("Location ID (blank=all)"),
                    Page = (int)Number("Page", 1),
                    PageSize = 20
                }));
    }

    private async Task ShowReferencesAsync(Session session)
    {
        ReferenceView.Show(await reports.ReferencesAsync(session));
    }

    private async Task RaiseFaultyEquipmentIncidentAsync(Session session)
    {
        ConsoleView.ShowMessage("Created service request " + await maintenance.CreateAsync(
                session,
                Number("Equipment ID"),
                TicketType.corrective,
                Choice<TicketPriority>("Priority"),
                Read("Title"),
                Read("Issue description"),
                Date("Requested due date")));
    }

    private async Task TicketLogsAsync(Session session)
    {
        TicketView.ShowTickets(await maintenance.ListAsync(session));
    }

    private async Task ShowTicketDetailsAsync(Session session)
    {
        TicketView.ShowDetails(await maintenance.DetailAsync(session, Number("Ticket ID")));
    }
}
