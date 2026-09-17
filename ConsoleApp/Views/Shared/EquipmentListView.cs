using EquipmentManagementBackend.DTOs;

namespace CriticalCare.ConsoleApp.Views.Shared;

public static class EquipmentListView
{
    public static void Show(PagedResult<EquipmentView> page)
    {
        Console.WriteLine($"Page {page.Page}/{page.TotalPages}, total {page.TotalCount}");
        foreach (var row in page.Items)
        {
            var equipment = row.Equipment;
            Console.WriteLine($"{equipment.Id} | {equipment.EquipmentCode} | {equipment.Name} | {row.Availability} | {equipment.Location?.Name} | PM {equipment.MaintenanceFrequency} next={equipment.NextMaintenanceDate} | CAL {equipment.CalibrationFrequency} next={equipment.NextCalibrationDate}");
        }
    }
}
