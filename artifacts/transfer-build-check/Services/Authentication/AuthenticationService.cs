using System.Security.Cryptography;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IApplicationRepository database;

    public AuthenticationService(IApplicationRepository database)
    {
        this.database = database;
    }

    public static readonly string[] Roles = ["admin", "approver", "technician", "staff"];
    public async Task<bool> NeedsBootstrapAsync()
    {
        await using var db = database.Open();
        return !await db.Users.AnyAsync(x => x.PasswordHash != null);
    }

    public async Task BootstrapAsync(string name, string email, string password)
    {
        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        if (await db.Users.AnyAsync(x => x.PasswordHash != null))
        {
            throw new InvalidOperationException("Bootstrap already completed. Log in as an administrator.");
        }

        ValidateIdentity(name, email);
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await db.Users.SingleOrDefaultAsync(x => x.Email.ToLower() == normalized);
        if (existing != null)
        {
            throw new InvalidOperationException("Choose a new administrator email. Existing identities cannot be claimed through bootstrap.");
        }

        var user = new User
        {
            Name = name.Trim(),
            Email = normalized,
            Role = "admin",
            PasswordHash = Hash(password)

        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        Database.Audit(
            db,
            user.Id,
            "Local console administrator bootstrapped",
            "User",
            user.Id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<Session> LoginAsync(string email, string password)
    {
        await using var db = database.Open();
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email.ToLower() == normalized);
        if (user == null || !user.IsActive || !Roles.Contains(user.Role) || !Verify(password, user.PasswordHash))
        {
            db.AuditLogs.Add(new AuditLog {
                    UserId = user?.Id,
                    EntityType = "Session",
                    Action = EquipmentManagementBackend.Models.Enums.AuditAction.login,
                    Description = "Console login failed",
                    Result = EquipmentManagementBackend.Models.Enums.AuditResult.failure
                });
            await db.SaveChangesAsync();
            throw new UnauthorizedAccessException("Invalid credentials or inactive account.");
        }

        db.AuditLogs.Add(new AuditLog {
                UserId = user.Id,
                EntityType = "Session",
                    Action = EquipmentManagementBackend.Models.Enums.AuditAction.login,
                Description = "Console login succeeded"
            });
        await db.SaveChangesAsync();
        return new Session(user.Id);
    }

    public async Task<User> RequireAsync(IUnitOfWork db, Session session, params string[] roles)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == session.UserId && x.IsActive);
        if (user == null || !roles.Contains(user.Role))
        {
            throw new UnauthorizedAccessException("Your account is not allowed to perform this action.");
        }

        db.ActorId = user.Id;
        return user;
    }

    public async Task<User> CurrentAsync(Session session)
    {
        await using var db = database.Open();
        return await RequireAsync(db, session, Roles);
    }

    public async Task ChangePasswordAsync(Session session, string currentPassword, string newPassword)
    {
        await using var db = database.Open();
        await using var transaction = await db.BeginInventoryAsync(default);
        await RequireAsync(db, session, Roles);
        var user = await db.Users.SingleAsync(x => x.Id == session.UserId);
        if (!Verify(currentPassword, user.PasswordHash))
            throw new ArgumentException("Current password is incorrect.");

        user.PasswordHash = Hash(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        Database.Audit(db, user.Id, "Password changed", "User", user.Id,
            EquipmentManagementBackend.Models.Enums.AuditAction.update);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public static void ValidateIdentity(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
        {
            throw new ArgumentException("Name is required (maximum 150 characters).");
        }

        if (string.IsNullOrWhiteSpace(email) || email.Length > 200 || !System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || parsed.Address != email.Trim())
        {
            throw new ArgumentException("A valid email address is required.");
        }
    }

    public static string Hash(string password)
    {
        if (password.Length < 10 || password.Length > 128)
        {
            throw new ArgumentException("Use a password between 10 and 128 characters.");
        }

        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            210000,
            HashAlgorithmName.SHA256,
            32);
        return $"210000:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(key)}";
    }

    private static bool Verify(string password, string? hash)
    {
        if (hash == null || password.Length > 128)
        {
            return false;
        }

        try
        {
            var parts = hash.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations != 210000)
            {
                return false;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                Convert.FromBase64String(parts[1]),
                iterations,
                HashAlgorithmName.SHA256,
                32);
            return CryptographicOperations.FixedTimeEquals(actual, Convert.FromBase64String(parts[2]));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
