using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;

namespace CriticalCare.ConsoleApp.Views.Shared;

public static class BackupView
{
    public static void ShowAllocationResult(BackupDetails details)
    {
        Console.WriteLine($"Request {details.Request.Id} | Ward: {details.Request.RequestedWard} | Bed: {details.Request.BedNumber}");

        if (details.Request.Status == BackupRequestStatus.cancelled)
        {
            Console.WriteLine("Allocation cancelled. The reserved equipment has been released.");
            return;
        }

        if (details.Reservation is not { } reservation)
        {
            Console.WriteLine($"No equipment allocated. Request status: {details.Request.Status}.");
            foreach (var escalation in details.Escalations.Where(item => item.ResolvedAt == null))
            {
                Console.WriteLine(escalation.Reason);
            }
            return;
        }

        var equipment = reservation.Equipment;
        var location = equipment.Location;
        Console.WriteLine($"Allocated equipment: {equipment.Id} | {equipment.EquipmentCode} | {equipment.Name}");
        Console.WriteLine($"Pickup location: {location?.Name} | Building {location?.Building} | Floor {location?.Floor} | Ward {location?.Ward} | Room {location?.Room} | Shelf {location?.Shelf}");

        if (reservation.Allocation.Status == BackupAllocationStatus.in_use)
        {
            Console.WriteLine("Pickup confirmed. The equipment is now in use.");
        }
        else
        {
            Console.WriteLine($"Reserved until {reservation.Allocation.ReservationExpiresAt:u} (UTC).");
        }
    }

    public static string ReadAllocationDecision()
    {
        while (true)
        {
            Console.WriteLine("1 Pick up / Confirm");
            Console.WriteLine("2 Cancel");
            var choice = ConsoleInput.Read("Choice");
            if (choice is "1" or "2")
            {
                return choice;
            }
            Console.WriteLine("Choose 1 to confirm pickup or 2 to cancel.");
        }
    }

    public static void ShowBackup(BackupDetails details)
    {
        Console.WriteLine($"Request {details.Request.Id} {details.Request.Status}: {details.Request.RequestedWard} bed {details.Request.BedNumber}, server {details.ServerTimeUtc:u}");
        if (details.Reservation is { } reservation)
        {
            var location = reservation.Equipment.Location;
            Console.WriteLine($"PICKUP: {location?.Name} | Building {location?.Building} | Floor {location?.Floor} | Ward {location?.Ward} | Room {location?.Room} | Shelf {location?.Shelf}");
            Console.WriteLine($"Collect by {reservation.Allocation.ReservationExpiresAt:u} (UTC); {Math.Max(0, reservation.RemainingSeconds):F0} seconds remaining. Use Mark picked up when collected.");
        }

        foreach (var a in details.AllocationHistory)
        {
            Console.WriteLine($"Allocation {a.Allocation.Id}: equipment {a.Equipment.Id} {a.Equipment.Name}, {a.Allocation.Status}, expiry {a.Allocation.ReservationExpiresAt:u}, seconds remaining {a.RemainingSeconds:F0}");
        }

        foreach (var e in details.Escalations)
        {
            Console.WriteLine($"Escalation {e.Id}: {e.Reason}, resolved={e.ResolvedAt}");
        }
    }

    public static void ShowList(PagedResult<BackupRequest> page)
    {
        foreach (var request in page.Items)
        {
            Console.WriteLine($"{request.Id} | {request.RequestNumber} | {request.Status} | {request.RequestedWard}/{request.BedNumber} | type {request.EquipmentTypeId}");
        }
    }
}
