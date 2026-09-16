using EquipmentManagementBackend.DTOs;

namespace CriticalCare.ConsoleApp;

public static class EquipmentDetailsView
{
    public static void Show(ManagedEquipmentDetails details)
    {
        Show(details.Inventory);
        Console.WriteLine("Technician: " + string.Join(", ", details.Technicians.Select(user => $"{user.Name} (ID {user.Id}, active={user.IsActive})")));
        Console.WriteLine("Approver: " + string.Join(", ", details.Approvers.Select(user => $"{user.Name} (ID {user.Id}, active={user.IsActive})")));
    }

    public static void Show(EquipmentView details)
    {
        var equipment = details.Equipment;
        Console.WriteLine($"Equipment {equipment.Id} | {equipment.EquipmentCode} | {equipment.Name}");
        Console.WriteLine($"Type: {equipment.EquipmentType?.TypeName} | Location: {equipment.Location?.Name}");
        Console.WriteLine($"Availability: {details.Availability} | Active: {equipment.IsActive}");
        Console.WriteLine($"Maintenance frequency: {equipment.MaintenanceFrequency} | Calibration frequency: {equipment.CalibrationFrequency}");
        Console.WriteLine($"Created: {equipment.CreatedAt:u} | Schedule anchor: {equipment.MaintenanceAnchorDate}");
        Console.WriteLine($"Next maintenance: {equipment.NextMaintenanceDate} | Next calibration: {equipment.NextCalibrationDate}");
        Console.WriteLine($"Serial: {equipment.SerialNumber} | Manufacturer: {equipment.Manufacturer} | Model: {equipment.ModelNumber}");
    }
}
