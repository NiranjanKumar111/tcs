using CriticalCare.ConsoleApp;

namespace EquipmentManagementBackend.Controllers;

public sealed class ExceptionDashboardController
{
    private readonly IExceptionDashboardService exceptions;

    public ExceptionDashboardController(IExceptionDashboardService exceptions)
    {
        this.exceptions = exceptions;
    }

    public async Task RunAsync(Session session)
    {
        while (true)
        {
            var report = await exceptions.GetAsync(session);
            var choice = ExceptionDashboardView.ReadOption(report);
            switch (choice)
            {
                case "0":
                    return;
                case "1":
                    ExceptionDashboardView.ShowRecords("Overdue maintenance", report.OverdueMaintenance);
                    break;
                case "2":
                    ExceptionDashboardView.ShowRecords("Unresolved tickets", report.UnresolvedTickets);
                    break;
                case "3":
                    ExceptionDashboardView.ShowRecords("Backup exceptions", report.BackupExceptions);
                    break;
                default:
                    ConsoleView.ShowMessage("Choose an option from the exception dashboard.");
                    break;
            }
        }
    }
}
