using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.DTOs;

namespace CriticalCare.ConsoleApp;

public static class TicketView
{
    public static void ShowTickets(IEnumerable<Ticket> tickets)
    {
        foreach (var ticket in tickets)
        {
            Console.WriteLine($"{ticket.TicketId} | {ticket.TicketType} | {ticket.Status} | {ticket.Title} | equipment={ticket.EquipmentId} | due={ticket.DueDate} | tech={ticket.AssignedTo} approver={ticket.ApproverId} | version={ticket.VersionNumber}");
        }
    }

    public static void ShowDetails(TicketDetails details)
    {
        var ticket = details.Ticket;
        Console.WriteLine($"Ticket {ticket.TicketId} | {ticket.TicketType} | {ticket.Status} | Version {ticket.VersionNumber}");
        Console.WriteLine(ticket.Title);
        Console.WriteLine(ticket.Description);
        Console.WriteLine($"Work: {ticket.WorkReport}");
        foreach (var item in details.Checklist)
        {
            var result = item.Passed ? "PASS" : "FAIL";
            Console.WriteLine($"CHECK {item.Description}: {result} {item.Notes}");
        }

        foreach (var reading in details.Readings)
        {
            Console.WriteLine($"READING {reading.Parameter}: {reading.Measured} {reading.Unit}, allowed {reading.Minimum}..{reading.Maximum}, pass={reading.Passed}");
        }

        foreach (var review in details.Reviews)
        {
            Console.WriteLine($"REVIEW {review.VerifiedAt:u} approver={review.ApproverId} approved={review.Approved}: {review.Notes}");
        }

        foreach (var entry in details.History)
        {
            Console.WriteLine($"HISTORY {entry.PerformedAt:u} user={entry.PerformedBy} {entry.Action}: {entry.Remarks}");
        }
    }
}
