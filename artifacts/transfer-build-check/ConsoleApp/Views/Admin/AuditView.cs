using EquipmentManagementBackend.Models;

namespace CriticalCare.ConsoleApp.Views.Admin;

public static class AuditView
{
    public static void Show(IReadOnlyList<AuditLog> rows)
    {
        Console.WriteLine($"AUDIT HISTORY | {rows.Count} records");
        Console.WriteLine("ID | Time (UTC) | User ID | Action | Entity / ID | Result | Description");
        if (rows.Count == 0)
        {
            Console.WriteLine("No audit records found.");
        }
        foreach (var row in rows)
        {
            Console.WriteLine($"{row.Id} | {row.CreatedAt:u} | {row.UserId?.ToString() ?? "System"} | {row.Action} | {row.EntityType}/{row.EntityId} | {row.Result} | {row.Description}");
        }
    }
}
