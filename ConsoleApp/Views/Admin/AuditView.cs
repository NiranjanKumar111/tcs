using EquipmentManagementBackend.Models;

namespace CriticalCare.ConsoleApp.Views.Admin;

public static class AuditView
{
    public static void Show(IReadOnlyList<AuditLog> rows)
    {
        ConsoleView.Heading($"AUDIT HISTORY | {rows.Count} records");

        if (rows.Count == 0)
        {
            Console.WriteLine("No audit records found.");
        }
        foreach (var row in rows)
        {
            Console.WriteLine($"  Record {row.Id} | {row.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC | {ConsoleView.Friendly(row.Result)}");
            ConsoleView.Field("Actor", row.UserId.HasValue ? $"Employee ID {row.UserId}" : "System");
            ConsoleView.Field("Action", ConsoleView.Friendly(row.Action));
            ConsoleView.Field("Record", $"{row.EntityType} / {row.EntityId}");
            ConsoleView.Field("Details", row.Description);
            Console.WriteLine(new string('-', 64));
        }
    }
}
