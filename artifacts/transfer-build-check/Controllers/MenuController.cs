using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.Views.Shared.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public abstract class MenuController
{
    private readonly IAuthenticationService auth;

    public MenuController(IAuthenticationService auth)
    {
        this.auth = auth;
    }

    protected async Task ChangePasswordAsync(Session session)
    {
        var current = Password("Current password");
        var replacement = Password("New password (10-128 characters)");
        var confirmation = Password("Confirm new password");
        if (replacement != confirmation)
            throw new ArgumentException("New passwords do not match.");
        await auth.ChangePasswordAsync(session, current, replacement);
        ConsoleView.ShowMessage("Password changed. Use your new password the next time you sign in.");
    }

    protected async Task RunMenuAsync(
        Session session,
        string role,
        string title,
        Dictionary<string, (string Label, Func<Task> Run)> actions)
    {
        while (true)
        {
            var user = await auth.CurrentAsync(session);
            if (user.Role != role)
            {
                throw new UnauthorizedAccessException("Your role changed; log in again.");
            }

            var choice = ConsoleView.ReadMenuChoice(
                user.Name,
                role,
                title,
                actions.Select(action => (action.Key, action.Value.Label)));
            if (choice == "0")
            {
                return;
            }

            if (!actions.TryGetValue(choice, out var selected))
            {
                ConsoleView.ShowMessage("Choose an option from your menu.");
                continue;
            }

            try
            {
                await selected.Run();
            }
            catch (EndOfStreamException)
            {
                throw;
            }
            catch (Exception error)
            {
                ConsoleView.ShowMessage("Action not completed: " + error.Message);
            }
        }
    }
}
