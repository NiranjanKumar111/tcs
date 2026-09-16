using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class BackupController : MenuController
{
    private readonly IBackupsService backups;
    private readonly IReportsService reports;

    public BackupController(IAuthenticationService auth, IBackupsService backups, IReportsService reports) : base(auth)
    {
        this.backups = backups;
        this.reports = reports;
    }

    public Task RunStaffAsync(Session session) => RunAsync(session, "staff");

    public Task RunAsync(Session session, string role) => RunMenuAsync(session, role, "Backup allocation", new()
    {
        ["1"] = ("Request backup allocation", () => RequestAndConfirmAsync(session)),
        ["2"] = ("Allocation history", () => AllocationHistoryAsync(session))
    });

    private async Task RequestAndConfirmAsync(Session session)
    {
        var ward = Choice<WardType>("Ward type");
        var equipmentTypeId = Number("Equipment type ID");
        var bedNumber = Read("Bed number");

        var details = await backups.RequestAsync(session, equipmentTypeId, ward, bedNumber, null);
        BackupView.ShowAllocationResult(details);

        if (details.Reservation?.Allocation.Status != BackupAllocationStatus.reserved)
        {
            return;
        }

        var choice = BackupView.ReadAllocationDecision();
        var action = choice == "1" ? "pickup" : "cancel";
        var result = await backups.ActionAsync(
            session,
            action,
            details.Request.Id,
            details.Reservation.Allocation.Id);

        BackupView.ShowAllocationResult(result);
    }

    private async Task AllocationHistoryAsync(Session session)
    {
        var pageNumber = 1;
        while (true)
        {
            var history = await backups.ListAsync(session, pageNumber, ownOnly: true);
            if (history.TotalCount == 0)
            {
                ConsoleView.ShowMessage("No backup requests found.");
                return;
            }

            BackupView.ShowList(history);
            if (pageNumber >= history.TotalPages)
            {
                return;
            }
            pageNumber++;
        }
    }

}
