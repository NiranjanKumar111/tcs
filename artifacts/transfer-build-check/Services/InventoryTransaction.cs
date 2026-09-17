using EquipmentManagementBackend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EquipmentManagementBackend.Services;

public static class InventoryTransaction
{
    // Shared across all app instances. Equipment edits, reservations and expiry use the same
    // transaction-scoped lock so checking availability and changing it is one atomic operation.
    public static async Task<IDbContextTransaction> BeginAsync(AppDbContext db, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7319462801)", ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }
}
