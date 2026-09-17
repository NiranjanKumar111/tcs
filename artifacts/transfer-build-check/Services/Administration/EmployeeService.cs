using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.Services;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class EmployeeService : IEmployeeService
{
    private readonly IApplicationRepository database;
    private readonly IAuthenticationService auth;

    public EmployeeService(IApplicationRepository database, IAuthenticationService auth)
    {
        this.database = database;
        this.auth = auth;
    }

    public async Task<long> SaveEmployeeAsync(
        Session actor,
        long? id,
        string name,
        string email,
        string role,
        bool active,
        string? password)
    {
        AuthenticationService.ValidateIdentity(name, email);
        if (!AuthenticationService.Roles.Contains(role))
        {
            throw new ArgumentException("Choose admin, approver, technician or staff.");
        }

        await using var db = database.Open();
        await using var tx = await db.BeginInventoryAsync(default);
        await auth.RequireAsync(db, actor, "admin");
        var user = id.HasValue ? await db.Users.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ArgumentException("Employee not found.") : new User();
        if (id == actor.UserId && (!active || role != "admin"))
        {
            throw new ArgumentException("You cannot disable or demote your own administrator account.");
        }

        if (id.HasValue && (!active || role != user.Role) && await db.Tickets.AnyAsync(x => (x.AssignedTo == id || x.ApproverId == id) && x.Status != TicketStatus.completed && x.Status != TicketStatus.cancelled))
        {
            throw new ArgumentException("Reassign this employee's open tickets before disabling or changing their role.");
        }

        var normalized = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email.ToLower() == normalized && x.Id != user.Id))
        {
            throw new ArgumentException("Email already exists.");
        }

        user.Name = name.Trim();
        user.Email = normalized;
        user.Role = role;
        user.IsActive = active;
        user.UpdatedAt = DateTime.UtcNow;
        if (password != null)
        {
            user.PasswordHash = AuthenticationService.Hash(password);
        }

        if (user.PasswordHash == null && active)
        {
            throw new ArgumentException("Set a password for this employee before enabling console login.");
        }

        if (!id.HasValue)
        {
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();
        Database.Audit(
            db,
            actor.UserId,
            id.HasValue ? "Employee updated" : "Employee created",
            "User",
            user.Id,
            id.HasValue ? AuditAction.update : AuditAction.create);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return user.Id;
    }

    public async Task DeleteEmployeeAsync(Session actor, long id)
    {
        await using var db = database.Open();
        await using var transaction = await db.BeginInventoryAsync();
        await auth.RequireAsync(db, actor, "admin");
        if (id == actor.UserId)
        {
            throw new ArgumentException("You cannot delete your own administrator account.");
        }
        var employee = await db.Users.FindAsync(id) ?? throw new ArgumentException("Employee not found.");
        if (await db.Tickets.AnyAsync(ticket => (ticket.AssignedTo == id || ticket.ApproverId == id) &&
            ticket.Status != TicketStatus.completed && ticket.Status != TicketStatus.cancelled))
        {
            throw new ArgumentException("Reassign this employee's open tickets before deleting their account.");
        }
        employee.IsActive = false;
        employee.UpdatedAt = DateTime.UtcNow;
        Database.Audit(db, actor.UserId, "Employee deleted; account disabled and history retained", "User", id, AuditAction.delete);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<User> GetEmployeeAsync(Session actor, long id)
    {
        await using var db = database.Open();
        await auth.RequireAsync(db, actor, "admin");
        var employee = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id)
            ?? throw new ArgumentException("Employee not found.");
        Database.Audit(db, actor.UserId, "Employee details viewed", "User", id, AuditAction.view);
        await db.SaveChangesAsync();
        return employee;
    }

    public async Task<List<User>> EmployeesAsync(Session actor)
    {
        await using var db = database.Open();
        await auth.RequireAsync(db, actor, "admin");
        var employees = await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Database.Audit(db, actor.UserId, "Employee records viewed", "User", 0, AuditAction.view);
        await db.SaveChangesAsync();
        return employees;
    }
}
