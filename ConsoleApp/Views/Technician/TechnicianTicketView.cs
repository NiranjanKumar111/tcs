namespace CriticalCare.ConsoleApp.Views.Technician;

public static class TechnicianTicketView
{
    public static string ReadResponse() => ConsoleInput.Read("Technician response / work report");

    public static (string Description, bool Passed, string Notes) ReadChecklist() =>
        (ConsoleInput.Read("Checklist item (same text updates it)"), ConsoleInput.Yes("Passed?"), ConsoleInput.Read("Notes"));

    public static (string Parameter, string Unit, decimal Minimum, decimal Maximum, decimal Measured) ReadMeasurement() =>
        (ConsoleInput.Read("Parameter"), ConsoleInput.Read("Unit"), ConsoleInput.Decimal("Allowed minimum"),
            ConsoleInput.Decimal("Allowed maximum"), ConsoleInput.Decimal("Measured value"));
}
