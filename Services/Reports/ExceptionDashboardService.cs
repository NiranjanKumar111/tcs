using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Application;

public sealed class ExceptionDashboardService : IExceptionDashboardService
{
    private readonly IApplicationRepository database;
    private readonly IAuthenticationService authentication;

    public ExceptionDashboardService(IApplicationRepository database, IAuthenticationService authentication)
    {
        this.database = database;
        this.authentication = authentication;
    }

    public async Task<ExceptionDashboardReport> GetAsync(Session actor)
    {
        await using var repository = database.Open();
        await authentication.RequireAsync(repository, actor, "admin");

        var generatedAt = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(generatedAt);
        var tickets = await repository.Tickets.AsNoTracking()
            .Include(ticket => ticket.Equipment)
            .Where(ticket => ticket.Status != TicketStatus.completed && ticket.Status != TicketStatus.cancelled)
            .OrderBy(ticket => ticket.DueDate)
            .ThenBy(ticket => ticket.TicketId)
            .ToListAsync();

        var unresolvedTickets = tickets.Select(ticket => new ExceptionRecord(
            ticket.TicketNumber,
            ticket.TicketType.ToString(),
            ticket.Equipment?.Name ?? "Equipment unavailable",
            ticket.Status.ToString(),
            ticket.Priority.ToString())).ToList();

        var overdueMaintenance = tickets
            .Where(ticket => ticket.DueDate < today &&
                (ticket.TicketType == TicketType.preventive || ticket.TicketType == TicketType.calibration))
            .Select(ticket => new ExceptionRecord(
                ticket.TicketNumber,
                ticket.TicketType.ToString(),
                ticket.Equipment?.Name ?? "Equipment unavailable",
                "overdue",
                ticket.Priority.ToString()))
            .ToList();

        var backupRows = await (
            from escalation in repository.BackupEscalations.AsNoTracking()
            join request in repository.BackupRequests.AsNoTracking() on escalation.RequestId equals request.Id
            join equipmentType in repository.EquipmentTypes.AsNoTracking() on request.EquipmentTypeId equals equipmentType.Id
            join equipment in repository.Equipment.AsNoTracking() on request.RequestedEquipmentId equals (long?)equipment.Id into preferredEquipment
            from equipment in preferredEquipment.DefaultIfEmpty()
            where escalation.ResolvedAt == null
            orderby escalation.CreatedAt, escalation.Id
            select new
            {
                request.RequestNumber,
                request.Status,
                request.Priority,
                EquipmentName = equipment == null ? equipmentType.TypeName + " (no equipment allocated)" : equipment.Name
            }).ToListAsync();

        var backupExceptions = backupRows.Select(request => new ExceptionRecord(
            request.RequestNumber,
            "backup",
            request.EquipmentName,
            request.Status.ToString(),
            request.Priority.ToString())).ToList();

        return new ExceptionDashboardReport(generatedAt, overdueMaintenance, unresolvedTickets, backupExceptions);
    }
}
