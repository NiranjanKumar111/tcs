using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;

namespace CriticalCare.ConsoleApp.Views.Staff;

public static class StaffTicketView
{
    public static (long EquipmentId, TicketType Type) ReadRequest() =>
        (ConsoleInput.Number("Equipment ID"), ConsoleInput.Choice<TicketType>("Ticket type"));

    public static void ShowCreated(long id, TicketDetails details)
    {
        ConsoleView.ShowMessage($"Created ticket {id}.");
        TicketView.ShowDetails(details);
    }
}
