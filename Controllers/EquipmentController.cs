using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class EquipmentController : MenuController
{
    private readonly IEquipmentManagementService equipment;
    private readonly IReportsService reports;

    public EquipmentController(IAuthenticationService authentication, IEquipmentManagementService equipment,
        IReportsService reports) : base(authentication)
    {
        this.equipment = equipment;
        this.reports = reports;
    }

    public Task RunAsync(Session session) => RunMenuAsync(session, "admin", "Equipment", new()
    {
        ["1"] = ("Add", () => SaveAsync(session, null)),
        ["2"] = ("Delete", () => DeleteAsync(session)),
        ["3"] = ("Edit", () => EditAsync(session)),
        ["4"] = ("View all equipment", () => ShowEquipmentAsync(session)),
        ["5"] = ("View specific equipment", () => ViewSpecificAsync(session))
    });

    private async Task ShowEquipmentAsync(Session session)
    {
        var pageNumber = 1;
        while (true)
        {
            var page = await reports.EquipmentAsync(session, new EquipmentQuery { Page = pageNumber, PageSize = 100, IsActive = null });
            EquipmentListView.Show(page);
            if (pageNumber >= page.TotalPages) return;
            pageNumber++;
        }
    }

    private async Task EditAsync(Session session)
    {
        await ShowEquipmentAsync(session);
        await SaveAsync(session, Number("Equipment ID to edit"));
    }

    private async Task DeleteAsync(Session session)
    {
        await ShowEquipmentAsync(session);
        await equipment.DeleteEquipmentAsync(session, Number("Equipment ID to delete"));
        ConsoleView.ShowMessage("Equipment deleted from active inventory. History retained.");
    }

    private async Task ViewSpecificAsync(Session session)
    {
        var idOrCode = Read("Equipment ID or code (for example EQ-100)");
        EquipmentDetailsView.Show(await equipment.GetEquipmentAsync(session, idOrCode));
    }

    private async Task SaveAsync(Session session, long? id)
    {
        var assignees = await equipment.GetAssigneesAsync(session);
        if (assignees.Technicians.Count == 0 || assignees.Approvers.Count == 0)
        {
            ConsoleView.ShowMessage("Create an active technician and approver under Employee before saving equipment.");
            return;
        }
        ReferenceView.Show(await reports.ReferencesAsync(session));
        var name = Read("Name");
        var type = Number("Equipment type ID");
        var location = Number("Location ID");
        var frequency = Choice<MaintenanceFrequency>("Maintenance and calibration frequency");
        var technician = EquipmentAssignmentView.ReadEmployee("Technician", assignees.Technicians);
        var approver = EquipmentAssignmentView.ReadEmployee("Approver", assignees.Approvers);
        var serial = Optional("Serial number (blank=keep/none)");
        var manufacturer = Optional("Manufacturer (blank=keep/none)");
        var model = Optional("Model (blank=keep/none)");
        var saved = await equipment.SaveEquipmentAsync(session, id,
            new SaveEquipmentInput(name, type, location, frequency, technician, approver, serial, manufacturer, model));
        ConsoleView.ShowMessage("Equipment saved.");
        EquipmentDetailsView.Show(await equipment.GetEquipmentAsync(session, saved.ToString()));
    }
}
