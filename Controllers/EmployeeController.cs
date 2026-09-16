using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class EmployeeController : MenuController
{
    private readonly IEmployeeService employees;

    public EmployeeController(IAuthenticationService authentication, IEmployeeService employees) : base(authentication)
    {
        this.employees = employees;
    }

    public Task RunAsync(Session session) => RunMenuAsync(session, "admin", "Employee", new()
    {
        ["1"] = ("Add", () => SaveAsync(session, null)),
        ["2"] = ("Delete", () => DeleteAsync(session)),
        ["3"] = ("Edit", () => EditAsync(session)),
        ["4"] = ("View all employees", () => ShowEmployeesAsync(session)),
        ["5"] = ("View specific employee", () => ViewSpecificAsync(session))
    });

    private async Task ShowEmployeesAsync(Session session)
    {
        EmployeeView.Show(await employees.EmployeesAsync(session));
    }

    private async Task ViewSpecificAsync(Session session)
    {
        var employee = await employees.GetEmployeeAsync(session, Number("Employee ID"));
        EmployeeView.Show(new[] { employee });
    }

    private async Task EditAsync(Session session)
    {
        EmployeeView.Show(await employees.EmployeesAsync(session));
        await SaveAsync(session, Number("Employee ID to edit"));
    }

    private async Task DeleteAsync(Session session)
    {
        EmployeeView.Show(await employees.EmployeesAsync(session));
        await employees.DeleteEmployeeAsync(session, Number("Employee ID to delete"));
        ConsoleView.ShowMessage("Employee deleted from active accounts. History retained.");
    }

    private async Task SaveAsync(Session session, long? id)
    {
        var name = Read("Name");
        var email = Read("Email");
        var role = Read("Role: admin/approver/technician/staff").ToLowerInvariant();
        var password = Password(id.HasValue ? "New password (blank=keep existing)" : "Password");
        var saved = await employees.SaveEmployeeAsync(session, id, name, email, role, true,
            password.Length == 0 ? null : password);
        ConsoleView.ShowMessage($"Employee {saved} saved.");
    }
}
