using EquipmentManagementBackend.Models.Enums;

namespace CriticalCare.ConsoleApp;

internal static class BackupFlowTests
{
    public static async Task RunAsync(
        Session staff,
        long equipmentTypeId,
        IAuthenticationService authentication,
        IBackupsService backups,
        IReportsService reports,
        Action<bool, string> check)
    {
        var controller = new BackupController(authentication, backups, reports);
        foreach (var decision in new[] { "1", "2" })
        {
            var originalInput = Console.In;
            var originalOutput = Console.Out;
            using var input = new StringReader($"1\nICU\n{equipmentTypeId}\nFLOW-{decision}\ninvalid\n{decision}\n0\n");
            using var output = new StringWriter();
            try
            {
                Console.SetIn(input);
                Console.SetOut(output);
                await controller.RunStaffAsync(staff);
            }
            finally
            {
                Console.SetIn(originalInput);
                Console.SetOut(originalOutput);
            }

            var request = (await backups.ListAsync(staff)).Items.Single(item => item.BedNumber == $"FLOW-{decision}");
            var details = await backups.DetailsAsync(staff, request.Id);
            var expectedStatus = decision == "1" ? BackupRequestStatus.in_use : BackupRequestStatus.cancelled;
            check(details.Request.Status == expectedStatus, $"staff allocation decision {decision} updates the displayed request");
            check(output.ToString().Contains("Allocated equipment:") &&
                output.ToString().Contains("1 Pick up / Confirm") &&
                output.ToString().Contains("Choose 1 to confirm pickup or 2 to cancel.") &&
                !output.ToString().Contains("Allocation ID:"),
                $"staff allocation decision {decision} needs no manual allocation ID");

            if (decision == "1")
            {
                await backups.ActionAsync(staff, "return", request.Id, details.Reservation!.Allocation.Id);
            }
        }

        var savedInput = Console.In;
        var savedOutput = Console.Out;
        using var historyInput = new StringReader("2\n0\n");
        using var historyOutput = new StringWriter();
        try
        {
            Console.SetIn(historyInput);
            Console.SetOut(historyOutput);
            await controller.RunStaffAsync(staff);
        }
        finally
        {
            Console.SetIn(savedInput);
            Console.SetOut(savedOutput);
        }

        var historyText = historyOutput.ToString();
        check(historyText.Contains("FLOW-1") && historyText.Contains("FLOW-2") &&
            !historyText.Contains("41 Request backup") && !historyText.Contains("Retry reservation"),
            "staff option 2 displays request history directly without a management submenu");
    }
}
