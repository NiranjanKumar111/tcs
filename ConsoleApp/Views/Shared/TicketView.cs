using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.DTOs;

namespace CriticalCare.ConsoleApp.Views.Shared;

public static class TicketView
{
    private static string EmployeeLabel(long? id, string? name) =>
        id.HasValue ? $"{name ?? "Employee"} (ID {id.Value})" : "Unassigned";

    public static void ShowTickets(IEnumerable<Ticket> tickets)
    {
        var rows = tickets.ToList();
        ConsoleView.Heading($"TICKETS | {rows.Count} records");
        if (rows.Count == 0) ConsoleView.ShowMessage("No tickets found.");
        foreach (var ticket in rows)
        {
            Console.WriteLine($"  {ticket.TicketId} | {ticket.TicketType} | {ConsoleView.Friendly(ticket.Status)}");
            ConsoleView.Field("Title", ticket.Title);
            ConsoleView.Field("Equipment ID", ticket.EquipmentId);
            ConsoleView.Field("Priority / Due", $"{ConsoleView.Friendly(ticket.Priority)} / {ticket.DueDate:yyyy-MM-dd}");
            ConsoleView.Field("Technician", EmployeeLabel(ticket.AssignedTo, null));
            ConsoleView.Field("Approver", EmployeeLabel(ticket.ApproverId, null));
            Console.WriteLine(new string('-', 64));
        }
    }

    public static void ShowDetails(TicketDetails details)
    {
        var ticket = details.Ticket;
        ConsoleView.Heading($"TICKET {ticket.TicketId} | {ConsoleView.Friendly(ticket.TicketType)}");
        ConsoleView.Field("Status", ConsoleView.Friendly(ticket.Status));
        ConsoleView.Field("Priority", ConsoleView.Friendly(ticket.Priority));
        ConsoleView.Field("Equipment ID", ticket.EquipmentId);
        ConsoleView.Field("Created (UTC)", ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        ConsoleView.Field("Due date (UTC)", ticket.DueDate.ToString("yyyy-MM-dd"));
        ConsoleView.Field("Technician", EmployeeLabel(ticket.AssignedTo, details.TechnicianName));
        ConsoleView.Field("Approver", EmployeeLabel(ticket.ApproverId, details.ApproverName));
        ConsoleView.Field("Title", ticket.Title);
        if (!string.IsNullOrWhiteSpace(ticket.Description)) ConsoleView.Field("Description", ticket.Description);
        ConsoleView.Section("Technician response");
        Console.WriteLine("  " + (string.IsNullOrWhiteSpace(ticket.WorkReport) ? "No response submitted yet." : ticket.WorkReport));
        if (details.Checklist.Count > 0)
        {
            ConsoleView.Section("Checklist evidence");
            foreach (var item in details.Checklist)
            {
                Console.WriteLine($"  [{(item.Passed ? "PASS" : "FAIL")}] {item.Description}");
                if (!string.IsNullOrWhiteSpace(item.Notes)) ConsoleView.Field("Notes", item.Notes);
            }
        }
        if (details.Readings.Count > 0)
        {
            ConsoleView.Section("Calibration measurements");
            foreach (var reading in details.Readings)
                Console.WriteLine($"  [{(reading.Passed ? "PASS" : "FAIL")}] {reading.Parameter}: {reading.Measured} {reading.Unit} (allowed {reading.Minimum} to {reading.Maximum})");
        }
        if (details.Reviews.Count > 0)
        {
            ConsoleView.Section("Approval decisions");
            foreach (var review in details.Reviews)
            {
                Console.WriteLine($"  {review.VerifiedAt:yyyy-MM-dd HH:mm:ss} UTC | {(review.Approved ? "ACCEPTED" : "REJECTED")} | Approver ID {review.ApproverId}");
                ConsoleView.Field(review.Approved ? "Review notes" : "Rejection reason", review.Notes);
            }
        }
        ConsoleView.Section("Activity history (UTC)");
        if (details.History.Count == 0) Console.WriteLine("  No activity recorded.");
        foreach (var entry in details.History)
        {
            Console.WriteLine($"  {entry.PerformedAt:yyyy-MM-dd HH:mm:ss} | {ConsoleView.Friendly(entry.Action)} | Employee ID {entry.PerformedBy}");
            if (entry.NewStatus.HasValue) ConsoleView.Field("Status after action", ConsoleView.Friendly(entry.NewStatus.Value));
            if (entry.NewAssignedTo.HasValue) ConsoleView.Field("Assigned technician", entry.NewAssignedTo.Value);
            if (!string.IsNullOrWhiteSpace(entry.Remarks)) ConsoleView.Field("Details", entry.Remarks);
        }
    }
}
