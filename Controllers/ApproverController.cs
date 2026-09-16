using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class ApproverController : MenuController, IRoleController
{
    private readonly IMaintenanceService maintenance;

    public ApproverController(IAuthenticationService auth, IMaintenanceService maintenance) : base(auth)
    {
        this.maintenance = maintenance;
    }

    public string Role => "approver";

    public Task RunAsync(Session session) => RunMenuAsync(
        session,
        "approver",
        "Approver",
        new()
        {
            ["1"] = ("Pending approvals", () => PendingApprovalsAsync(session)),
            ["2"] = ("Review pending ticket", () => ReviewEvidenceAndApproveRejectAsync(session)),
            ["3"] = ("Ticket details/history", () => ShowTicketDetailsAsync(session)),
            ["4"] = ("Close approved ticket", () => CloseApprovedTicketAsync(session)),
            ["5"] = ("Change password", () => ChangePasswordAsync(session)),

        });
    private async Task PendingApprovalsAsync(Session session)
    {
        var tickets = await maintenance.ListAsync(session, true);
        if (tickets.Count == 0) ConsoleView.ShowMessage("No pending approvals.");
        else TicketView.ShowTickets(tickets);
    }

    private async Task ShowTicketDetailsAsync(Session session)
    {
        TicketView.ShowDetails(await maintenance.DetailAsync(session, Number("Ticket ID")));
    }

    private async Task ReviewEvidenceAndApproveRejectAsync(Session session)
    {
        var id = Number("Ticket ID");
        var details = await maintenance.DetailAsync(session, id);
        if (details.Ticket.Status != TicketStatus.pending_approval)
            throw new ArgumentException("Only pending approvals can be reviewed.");
        TicketView.ShowDetails(details);
        ConsoleView.ShowMessage("1 Accept\n2 Reject\n0 Back");
        var choice = Read("Decision");
        if (choice == "0") return;
        if (choice != "1" && choice != "2")
            throw new ArgumentException("Choose 1 to accept or 2 to reject.");
        var accepted = choice == "1";
        string notes;
        if (accepted)
            notes = Optional("Review notes (optional)") ?? "Accepted technician response";
        else
        {
            do
            {
                notes = Read("Rejection reason (required)");
                if (string.IsNullOrWhiteSpace(notes)) ConsoleView.ShowMessage("A rejection reason is required.");
            } while (string.IsNullOrWhiteSpace(notes));
        }
        await maintenance.ReviewAsync(session, id, details.Ticket.VersionNumber, accepted, notes);
        ConsoleView.ShowMessage(accepted ? "Ticket accepted." : "Ticket rejected and returned to its assigned technician for rework.");
    }

    private async Task CloseApprovedTicketAsync(Session session)
    {
        var details = await maintenance.DetailAsync(session, Number("Ticket ID"));
        await maintenance.CloseAsync(session, details.Ticket.TicketId, details.Ticket.VersionNumber);
    }
}
