using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.Views.Shared.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class StaffController : MenuController, IRoleController
{
    private readonly IncidentController incidents;
    private readonly BackupController backups;

    public StaffController(IAuthenticationService auth, IncidentController incidents, BackupController backups) : base(auth)
    {
        this.incidents = incidents;
        this.backups = backups;
    }

    public string Role => "staff";

    public Task RunAsync(Session session) => RunMenuAsync(
        session,
        Role,
        "Staff",
        new()
        {
            ["1"] = ("Ticket", () => incidents.RunAsync(session)),
            ["2"] = ("Backup allocation", () => backups.RunStaffAsync(session)),
            ["3"] = ("Change password", () => ChangePasswordAsync(session))
        });
}
