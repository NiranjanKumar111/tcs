using EquipmentManagementBackend.DTOs;

namespace CriticalCare.ConsoleApp.Views.Approver;

public static class ApproverTicketView
{
    public static (bool Accepted, string Notes)? ReadReview(TicketDetails details)
    {
        TicketView.ShowDetails(details);
        ConsoleView.ShowMessage("1 Accept\n2 Reject\n0 Back");
        var choice = ConsoleInput.Read("Decision");
        if (choice == "0") return null;
        if (choice != "1" && choice != "2")
            throw new ArgumentException("Choose 1 to accept or 2 to reject.");
        if (choice == "1")
            return (true, ConsoleInput.Optional("Review notes (optional)") ?? "Accepted technician response");
        string reason;
        do
        {
            reason = ConsoleInput.Read("Rejection reason (required)");
            if (string.IsNullOrWhiteSpace(reason)) ConsoleView.ShowMessage("A rejection reason is required.");
        } while (string.IsNullOrWhiteSpace(reason));
        return (false, reason);
    }
}
