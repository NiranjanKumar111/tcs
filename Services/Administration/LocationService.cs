using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class LocationService : ILocationService
{
    private readonly IApplicationRepository database;
    private readonly IAuthenticationService auth;

    public LocationService(IApplicationRepository database, IAuthenticationService auth)
    {
        this.database = database;
        this.auth = auth;
    }

    public async Task<long> SaveLocationAsync(
        Session actor,
        long? id,
        string code,
        string name,
        string building,
        string floor,
        WardType ward,
        string? room,
        string? shelf,
        bool warehouse)
    {
        InputValidation.RequireText(code, 100, "Code");
        InputValidation.RequireText(name, 200, "Name");
        InputValidation.RequireText(building, 150, "Building");
        InputValidation.RequireText(floor, 100, "Floor");
        if (!Enum.IsDefined(ward) || room?.Length > 150 || shelf?.Length > 150)
        {
            throw new ArgumentException("Invalid ward, room or shelf.");
        }

        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        await auth.RequireAsync(db, actor, "admin");
        var location = id.HasValue ? await db.Locations.FindAsync(id.Value) ?? throw new ArgumentException("Location not found.") : new Location();
        if (id.HasValue && location.IsCentralWarehouse != warehouse && await db.BackupAllocations.AnyAsync(a => (a.Status == BackupAllocationStatus.reserved || a.Status == BackupAllocationStatus.in_use) && db.Equipment.Any(e => e.Id == a.EquipmentId && e.LocationId == id)))
        {
            throw new ArgumentException("Location has occupied equipment; warehouse flag cannot change yet.");
        }

        location.Code = code.Trim();
        location.Name = name.Trim();
        location.Building = building.Trim();
        location.Floor = floor.Trim();
        location.Ward = ward;
        location.Room = room;
        location.Shelf = shelf;
        location.IsCentralWarehouse = warehouse;
        location.UpdatedAt = DateTime.UtcNow;
        location.UpdatedBy = actor.UserId;
        if (!id.HasValue)
        {
            location.CreatedBy = actor.UserId;
            db.Locations.Add(location);
        }

        await db.SaveChangesAsync();
        Database.Audit(
            db,
            actor.UserId,
            "Location saved",
            "Location",
            location.Id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return location.Id;
    }
}
