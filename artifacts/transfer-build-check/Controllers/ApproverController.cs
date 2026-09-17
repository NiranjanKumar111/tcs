using CriticalCare.ConsoleApp.Views.Approver;
using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.Views.Shared.ConsoleInput;

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
        var review = ApproverTicketView.ReadReview(details);
        if (review == null) return;
        var (accepted, notes) = review.Value;
        await maintenance.ReviewAsync(session, id, details.Ticket.VersionNumber, accepted, notes);
        ConsoleView.ShowMessage(accepted ? "Ticket accepted." : "Ticket rejected and returned to its assigned technician for rework.");
    }

    private async Task CloseApprovedTicketAsync(Session session)
    {
        var details = await maintenance.DetailAsync(session, Number("Ticket ID"));
        await maintenance.CloseAsync(session, details.Ticket.TicketId, details.Ticket.VersionNumber);
    }
}
