using EquipmentManagementBackend.Models.Enums;

namespace EquipmentManagementBackend.Application;

public static class Schedule
{
    public static DateOnly? Next(DateOnly anchor, MaintenanceFrequency frequency) => frequency switch
    {
        MaintenanceFrequency.none => null,
        MaintenanceFrequency.daily => anchor.AddDays(1),
        MaintenanceFrequency.weekly => anchor.AddDays(7),
        MaintenanceFrequency.monthly => anchor.AddMonths(1),
        MaintenanceFrequency.quarterly => anchor.AddMonths(3),
        MaintenanceFrequency.half_yearly => anchor.AddMonths(6),
        MaintenanceFrequency.yearly => anchor.AddYears(1),
        _ => throw new ArgumentException("Unknown maintenance frequency.")};
}
