using EquipmentManagementBackend.Models;

namespace CriticalCare.ConsoleApp.Views.Admin;

public static class EquipmentAssignmentView
{
    public static long ReadEmployee(string role, IReadOnlyList<User> employees)
    {
        Console.WriteLine($"Available {role}s:");
        foreach (var employee in employees)
        {
            Console.WriteLine($"{employee.Id}: {employee.Name} | {employee.Email}");
        }
        while (true)
        {
            var name = ConsoleInput.Read($"{role} name");
            var matches = employees.Where(employee => employee.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 1) return matches[0].Id;
            if (matches.Count == 0)
            {
                Console.WriteLine($"Enter the full name of a listed {role}.");
                continue;
            }
            Console.WriteLine("Several employees have this name:");
            foreach (var employee in matches)
            {
                Console.WriteLine($"{employee.Id}: {employee.Name} | {employee.Email}");
            }
            var id = ConsoleInput.Number("Employee ID for that name");
            if (matches.Any(employee => employee.Id == id)) return id;
            Console.WriteLine("The ID must belong to one of the matching employees.");
        }
    }
}
