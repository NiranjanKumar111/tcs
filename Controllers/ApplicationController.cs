using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.DTOs;
using EquipmentManagementBackend.Models.Enums;
using static CriticalCare.ConsoleApp.ConsoleInput;

namespace EquipmentManagementBackend.Controllers;

public sealed class ApplicationController
{
    private readonly IAuthenticationService auth;
    private readonly IReadOnlyList<IRoleController> menus;

    public ApplicationController(IAuthenticationService auth, IReadOnlyList<IRoleController> menus)
    {
        this.auth = auth;
        this.menus = menus;
    }

    public async Task RunAsync()
    {
        ConsoleView.ShowMessage("CRITICAL CARE COMPLIANCE | Console + PostgreSQL | No web server");
        if (await auth.NeedsBootstrapAsync())
        {
            ConsoleView.ShowMessage("First launch: create a new administrator account. Existing accounts are not claimed automatically.");
            while (true)
            {
                try
                {
                    await auth.BootstrapAsync(Read("Admin name"), Read("New admin email"), Password("Admin password (10+ characters)"));
                    break;
                }
                catch (EndOfStreamException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    ConsoleView.ShowMessage(ex.Message);
                }
            }
        }

        while (true)
        {
            Session session;
            try
            {
                ConsoleView.ShowMessage("\nLOGIN (enter 'exit' as email to quit)");
                var email = Read("Email");
                if (email.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                session = await auth.LoginAsync(email, Password("Password"));
            }
            catch (EndOfStreamException)
            {
                return;
            }
            catch (Exception ex)
            {
                ConsoleView.ShowMessage(ex.Message);
                await Task.Delay(1000);
                continue;
            }

            try
            {
                var user = await auth.CurrentAsync(session);
                await menus.Single(x => x.Role == user.Role).RunAsync(session);
            }
            catch (EndOfStreamException)
            {
                return;
            }
            catch (Exception error)
            {
                ConsoleView.ShowMessage(error.Message);
            }
        }
    }
}
