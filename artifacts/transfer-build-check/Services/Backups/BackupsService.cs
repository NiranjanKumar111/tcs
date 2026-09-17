using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class BackupsService : IBackupsService
{
    private readonly IApplicationLogger? logger;
    private readonly IApplicationRepository database;
    private readonly IAuthenticationService auth;

    public BackupsService(IApplicationRepository database, IAuthenticationService auth, IApplicationLogger? logger = null)
    {
        this.logger = logger;
        this.database = database;
        this.auth = auth;
    }

    public async Task<BackupDetails> RequestAsync(
        Session actor,
        long type,
        WardType ward,
        string bed,
        long? preferred)
    {
        await using var db = database.Open();
        await auth.RequireAsync(
            db,
            actor,
            "staff",
            "admin");
        return await new BackupReservationService(db, TimeProvider.System).CreateAsync(new(
                ward,
                actor.UserId,
                type,
                bed,
                preferred), default);
    }

    public async Task<BackupDetails> DetailsAsync(Session actor, long requestId)
    {
        await using var db = database.Open();
        var user = await auth.RequireAsync(
            db,
            actor,
            "staff",
            "admin");
        if (!await db.BackupRequests.AnyAsync(x => x.Id == requestId && (user.Role == "admin" || x.RequestedBy == actor.UserId)))
        {
            throw new UnauthorizedAccessException("Request not found or belongs to another staff member.");
        }

        return await new BackupReservationService(db, TimeProvider.System).GetAsync(requestId, default);
    }

    public async Task<PagedResult<BackupRequest>> ListAsync(Session actor, int page = 1, bool ownOnly = false)
    {
        await using var db = database.Open();
        var user = await auth.RequireAsync(
            db,
            actor,
            "staff",
            "admin");
        IBackupReservationService service = new BackupReservationService(db, TimeProvider.System);
        var rows = await service.ListAsync(new BackupQuery {
                RequestedBy = user.Role == "admin" && !ownOnly ? null : actor.UserId,
                Page = page,
                  PageSize = 100
            }, default);
        return rows;
    }

    public async Task<BackupDetails> ActionAsync(
        Session actor,
        string action,
        long id,
        long allocationId = 0)
    {
        await using var db = database.Open();
        await auth.RequireAsync(
            db,
            actor,
            "staff",
            "admin");
        IBackupReservationService service = new BackupReservationService(db, TimeProvider.System);
        return action switch
        {
            "reserve" => await service.ReserveAsync(id, actor.UserId, default),
            "pickup" => await service.PickupAsync(
                id,
                allocationId,
                actor.UserId,
                default),
            "return" => await service.ReturnAsync(
                id,
                allocationId,
                actor.UserId,
                default),
            "cancel" => await service.CancelAsync(id, new(actor.UserId, "Cancelled from console"), default),
            _ => throw new ArgumentException("Unknown backup action.")};
    }

    public async Task RunExpiryAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            do
            {
                try
                {
                    await using var db = database.Open();
                    await new BackupReservationService(db, TimeProvider.System).ExpireAsync(cancellation);
                }
                catch (OperationCanceledException)when (cancellation.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger?.Error("Reservation expiry retry required", ex);
                }
            }
            while (await timer.WaitForNextTickAsync(cancellation));
        }
        catch (OperationCanceledException)when (cancellation.IsCancellationRequested)
        {
        }
    }
}
