using EquipmentManagementBackend.Models;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;

namespace CriticalCare.ConsoleApp;

public static class DashboardView
{
    public static void Show(DashboardReport report)
    {
        Console.WriteLine($"As of {report.GeneratedAt:u}");
        Console.WriteLine($"Active equipment: {report.ActiveEquipmentCount}");
        Console.WriteLine($"Open tickets: {report.OpenTicketCount}");
        Console.WriteLine($"Pending approvals: {report.PendingApprovalCount}");
        Console.WriteLine($"Overdue tickets: {report.OverdueTicketCount}");
        Console.WriteLine($"Unresolved escalations: {report.OpenEscalationCount}");
        Console.WriteLine($"PM verified/current: {Rate(report.VerifiedMaintenanceCount, report.ConfiguredMaintenanceCount)}");
        Console.WriteLine($"Calibration verified/current: {Rate(report.VerifiedCalibrationCount, report.ConfiguredCalibrationCount)}");
        Console.WriteLine($"PM overdue: {report.OverdueMaintenanceCount}; calibration overdue: {report.OverdueCalibrationCount}");
        if (!report.IncludesExceptions)
        {
            return;
        }

        Console.WriteLine("EXCEPTIONS");
        foreach (var equipment in report.EquipmentExceptions)
        {
            Console.WriteLine($"Equipment {equipment.Id} {equipment.Name}: PM due={equipment.NextMaintenanceDate}, last verified={equipment.LastMaintenanceDate}, CAL due={equipment.NextCalibrationDate}; check missing/unverified schedules");
        }

        foreach (var ticket in report.TicketExceptions)
        {
            Console.WriteLine($"Ticket {ticket.TicketId}: {ticket.Title}, {ticket.Status}, priority={ticket.Priority}, due={ticket.DueDate}, technician={ticket.AssignedTo}, approver={ticket.ApproverId}");
        }

        foreach (var escalation in report.Escalations)
        {
            Console.WriteLine($"Escalation {escalation.Id}, request {escalation.RequestId}, admin {escalation.AssignedAdminId}: {escalation.Reason}");
        }
    }

    private static string Rate(int verified, int configured)
    {
        return configured == 0 ? "N/A (no configured items)" : $"{verified}/{configured} = {100.0 * verified / configured:F1}%";
    }
}
