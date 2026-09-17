using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;

namespace CriticalCare.ConsoleApp.Views.Admin;

public static class ReferenceView
{
    public static void Show(ReferenceData data)
    {
        var types = data.EquipmentTypes;
        var locations = data.Locations;
        Console.WriteLine(Format(types, locations));
    }

    private static string Format(IReadOnlyList<EquipmentType> types, IReadOnlyList<Location> locations)
    {
        return "TYPES\n" + string.Join('\n', types.Select(x => $"{x.Id}: {x.TypeName}")) + "\nLOCATIONS\n" + string.Join('\n', locations.Select(x => $"{x.Id}: {x.Name} | {x.Building}, Floor {x.Floor}, {x.Ward}, Room {x.Room}, Shelf {x.Shelf}, Warehouse={x.IsCentralWarehouse}"));
    }
}
