using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.Models.Enums;
using EquipmentManagementBackend.DTOs;

namespace EquipmentManagementBackend.Application;

public interface IMaintenanceService
{
    Task<long> CreateAsync(
        Session actor,
        long equipmentId,
        TicketType type,
        TicketPriority priority,
        string title,
        string description,
        DateOnly due,
        long? technician = null,
        long? approver = null);
    Task ReassignTechnicianAsync(Session actor, long ticketId, int version, long technician);
    Task AssignAsync(
        Session actor,
        long ticketId,
        int version,
        long technician,
        long approver);
    Task ProgressAsync(
        Session actor,
        long id,
        int version,
        string notes);
    Task ChecklistAsync(
        Session actor,
        long id,
        int version,
        string description,
        bool passed,
        string notes);
    Task CalibrationAsync(
        Session actor,
        long id,
        int version,
        string parameter,
        string unit,
        decimal minimum,
        decimal maximum,
        decimal measured);
    Task SubmitAsync(
        Session actor,
        long id,
        int version,
        DateOnly completedOn);
    Task ReviewAsync(
        Session actor,
        long id,
        int version,
        bool approve,
        string notes);
    Task CloseAsync(Session actor, long id, int version);
    Task<List<Ticket>> ListAsync(Session actor, bool pendingOnly = false, long? equipmentId = null);
    Task<TicketDetails> DetailAsync(Session actor, long id);
}
