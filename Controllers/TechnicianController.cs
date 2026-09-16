using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class TechnicianController : MenuController, IRoleController
{
    private readonly IMaintenanceService maintenance;
    private readonly IReportsService reports;

    public TechnicianController(IAuthenticationService auth, IMaintenanceService maintenance, IReportsService reports) : base(auth)
    {
        this.maintenance = maintenance;
        this.reports = reports;
    }

    public string Role => "technician";

    public Task RunAsync(Session session) => RunMenuAsync(session, Role, "Technician", new()
    {
        ["1"] = ("Equipment", () => EquipmentMenuAsync(session)),
        ["2"] = ("Assigned tickets", () => TicketsMenuAsync(session)),
        ["3"] = ("Change password", () => ChangePasswordAsync(session))
    });

    private Task EquipmentMenuAsync(Session session) => RunMenuAsync(session, Role, "Equipment", new()
    {
        ["1"] = ("View all assigned equipment", () => ShowEquipmentAsync(session)),
        ["2"] = ("View equipment by ID", () => ShowEquipmentDetailsAsync(session))
    });

    private Task TicketsMenuAsync(Session session) => RunMenuAsync(session, Role, "Assigned tickets", new()
    {
        ["1"] = ("View assigned tickets", () => TicketLogsAsync(session)),
        ["2"] = ("Update ticket", () => UpdateTicketAsync(session))
    });

    private async Task ShowEquipmentAsync(Session session)
    {
        var number = 1;
        while (true)
        {
            var page = await reports.EquipmentAsync(session, new EquipmentQuery { Page = number, PageSize = 100, IsActive = null });
            EquipmentListView.Show(page);
            if (number >= page.TotalPages) return;
            number++;
        }
    }

    private async Task ShowEquipmentDetailsAsync(Session session)
    {
        EquipmentDetailsView.Show(await reports.TechnicianEquipmentAsync(session, Number("Equipment ID")));
    }

    private async Task TicketLogsAsync(Session session)
    {
        TicketView.ShowTickets(await maintenance.ListAsync(session));
    }

    private async Task UpdateTicketAsync(Session session)
    {
        var id = Number("Ticket ID");
        var details = await maintenance.DetailAsync(session, id);
        TicketView.ShowDetails(details);
        if (details.Ticket.Status is not (TicketStatus.open or TicketStatus.in_progress or TicketStatus.rejected or TicketStatus.overdue))
        {
            ConsoleView.ShowMessage("This ticket cannot be updated while awaiting approval or after approval/closure.");
            return;
        }
        await maintenance.ProgressAsync(session, id, details.Ticket.VersionNumber, Read("Technician response / work report"));
        ConsoleView.ShowMessage("Response saved. Complete the required evidence before sending for approval.");
        details = await maintenance.DetailAsync(session, id);
        var recordChecklist = details.Checklist.Count == 0 || Yes("Add or update checklist evidence?");
        while (recordChecklist)
        {
            await maintenance.ChecklistAsync(session, id, details.Ticket.VersionNumber,
                Read("Checklist item (same text updates it)"), Yes("Passed?"), Read("Notes"));
            details = await maintenance.DetailAsync(session, id);
            recordChecklist = Yes("Add or update another checklist item?");
        }
        if (details.Ticket.TicketType == TicketType.calibration)
        {
            var recordMeasurement = details.Readings.Count == 0 || Yes("Add or update calibration measurements?");
            while (recordMeasurement)
            {
                await maintenance.CalibrationAsync(session, id, details.Ticket.VersionNumber,
                    Read("Parameter"), Read("Unit"), Decimal("Allowed minimum"),
                    Decimal("Allowed maximum"), Decimal("Measured value"));
                details = await maintenance.DetailAsync(session, id);
                recordMeasurement = Yes("Add or update another measurement?");
            }
        }
        if (!Yes("Send to assigned approver for approval?"))
        {
            ConsoleView.ShowMessage("Work saved. Use Update ticket to continue later.");
            return;
        }
        await maintenance.SubmitAsync(session, id, details.Ticket.VersionNumber, Date("Work completed date"));
        ConsoleView.ShowMessage("Ticket sent to its assigned approver for approval.");
    }
}
