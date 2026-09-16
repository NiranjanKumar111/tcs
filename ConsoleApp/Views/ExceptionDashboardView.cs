using EquipmentManagementBackend.DTOs;

namespace CriticalCare.ConsoleApp;

public static class ExceptionDashboardView
{
    public static string ReadOption(ExceptionDashboardReport report)
    {
        Console.WriteLine($"\nEXCEPTION DASHBOARD | {report.GeneratedAt:u}");
        Console.WriteLine($"1 Overdue maintenance ({report.OverdueMaintenance.Count})");
        Console.WriteLine($"2 Unresolved tickets ({report.UnresolvedTickets.Count})");
        Console.WriteLine($"3 Backup exception counts ({report.BackupExceptions.Count})");
        Console.WriteLine("0 Back");
        return ConsoleInput.Read("Choice");
    }

    public static void ShowRecords(string title, IReadOnlyList<ExceptionRecord> records)
    {
        Console.WriteLine($"\n{title} | Count: {records.Count}");
        Console.WriteLine("Exception records");
        if (records.Count == 0)
        {
            Console.WriteLine("No exception records found.");
            return;
        }

        var rows = records.Select(record => new[]
        {
            record.TicketId, record.Type, record.EquipmentName, record.Status, record.Priority
        }).ToList();
        var headers = new[] { "Ticket ID", "Type", "Equipment name", "Status", "Priority" };
        var widths = headers.Select((header, index) => Math.Max(header.Length,
            rows.Max(row => Clean(row[index]).Length))).ToArray();

        void PrintRow(string[] row)
        {
            Console.WriteLine(string.Join(" | ", row.Select((cell, index) => Clean(cell).PadRight(widths[index]))));
        }

        PrintRow(headers);
        Console.WriteLine(string.Join("-+-", widths.Select(width => new string('-', width))));
        foreach (var row in rows)
        {
            PrintRow(row);
        }
    }

    private static string Clean(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
}
