using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.Views.Shared.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class AdminController : MenuController, IRoleController
{
    private readonly IReportsService reports;
    private readonly EmployeeController employees;
    private readonly EquipmentController equipment;
    private readonly AdminTicketController tickets;
    private readonly BackupController backups;
    private readonly ExceptionDashboardController exceptions;

    public AdminController(IAuthenticationService authentication, IReportsService reports,
        EmployeeController employees, EquipmentController equipment, AdminTicketController tickets,
        BackupController backups, ExceptionDashboardController exceptions) : base(authentication)
    {
        this.reports = reports;
        this.employees = employees;
        this.equipment = equipment;
        this.tickets = tickets;
        this.backups = backups;
        this.exceptions = exceptions;
    }

    public string Role => "admin";

    public Task RunAsync(Session session) => RunMenuAsync(session, Role, "Admin", new()
    {
        ["1"] = ("Dashboard KPI", () => ShowDashboardAsync(session)),
        ["2"] = ("Employee", () => employees.RunAsync(session)),
        ["3"] = ("Equipment", () => equipment.RunAsync(session)),
        ["4"] = ("Ticket", () => tickets.RunAsync(session)),
        ["5"] = ("Backup allocation", () => backups.RunAsync(session, Role)),
        ["6"] = ("Exception dashboard", () => exceptions.RunAsync(session)),
        ["7"] = ("Audit logs", () => ShowAuditAsync(session)),
        ["8"] = ("Change password", () => ChangePasswordAsync(session))
    });

    private async Task ShowDashboardAsync(Session session)
    {
        DashboardView.Show(await reports.DashboardAsync(session));
    }

    private async Task ShowAuditAsync(Session session)
    {
        AuditView.Show(await reports.AuditAsync(session));
    }
}
