using EquipmentManagementBackend.Data;
using EquipmentManagementBackend.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EquipmentManagementBackend.Services;

public class AssignmentService(AppDbContext db)
{
    private static readonly TicketStatus[] TechActive = [TicketStatus.open, TicketStatus.in_progress, TicketStatus.pending_approval, TicketStatus.overdue];
    private static readonly TicketStatus[] ApproverActive = [TicketStatus.open, TicketStatus.in_progress, TicketStatus.pending_approval];

    public async Task<long?> GetLeastLoadedTechnicianAsync(long equipmentId)
    {
        var candidates = db.EquipmentTechnicians.Where(x => x.EquipmentId == equipmentId && x.IsActive);
        return await candidates
            .Select(x => new { x.TechnicianId, Load = db.Tickets.Count(t => t.AssignedTo == x.TechnicianId && TechActive.Contains(t.Status)) })
            .OrderBy(x => x.Load).ThenBy(x => x.TechnicianId)
            .Select(x => (long?)x.TechnicianId)
            .FirstOrDefaultAsync();
    }

    public async Task<long?> GetLeastLoadedApproverAsync(long equipmentId)
    {
        var candidates = db.EquipmentApprovers.Where(x => x.EquipmentId == equipmentId && x.IsActive);
        return await candidates
            .Select(x => new { x.ApproverId, Load = db.Tickets.Count(t => t.ApproverId == x.ApproverId && ApproverActive.Contains(t.Status)) })
            .OrderBy(x => x.Load).ThenBy(x => x.ApproverId)
            .Select(x => (long?)x.ApproverId)
            .FirstOrDefaultAsync();
    }
}
