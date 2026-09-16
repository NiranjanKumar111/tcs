using EquipmentManagementBackend.Models;

namespace CriticalCare.ConsoleApp;

public static class EmployeeView
{
    public static void Show(IEnumerable<User> employees)
    {
        foreach (var employee in employees)
        {
            Console.WriteLine($"{employee.Id} | {employee.Name} | {employee.Email} | {employee.Role} | active={employee.IsActive} | login configured={employee.PasswordHash != null}");
        }
    }
}
