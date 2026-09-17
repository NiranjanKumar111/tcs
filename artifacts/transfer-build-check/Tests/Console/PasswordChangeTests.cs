using Microsoft.EntityFrameworkCore;

namespace CriticalCare.ConsoleApp;

internal static class PasswordChangeTests
{
    public static async Task RunAsync(Database database, IAuthenticationService auth,
        Session admin, Action<bool, string> check)
    {
        const string original = "Original-Password-482!";
        const string replacement = "Replacement-Password-593!";
        var employees = new EmployeeService(database, auth);
        foreach (var role in AuthenticationService.Roles)
        {
            var email = $"password-{role}@test.local";
            var id = await employees.SaveEmployeeAsync(admin, null, $"Password {role}", email, role, true, original);
            var session = await auth.LoginAsync(email, original);
            var rejected = false;
            try { await auth.ChangePasswordAsync(session, "Incorrect password", replacement); }
            catch (ArgumentException) { rejected = true; }
            check(rejected && (await auth.LoginAsync(email, original)).UserId == id,
                $"{role}: incorrect current password leaves credentials unchanged");
            rejected = false;
            try { await auth.ChangePasswordAsync(session, original, "short"); }
            catch (ArgumentException) { rejected = true; }
            check(rejected && (await auth.LoginAsync(email, original)).UserId == id,
                $"{role}: invalid new password leaves credentials unchanged");

            IRoleController controller = role switch
            {
                "admin" => new AdminController(auth, null!, null!, null!, null!, null!, null!),
                "staff" => new StaffController(auth, null!, null!),
                "technician" => new TechnicianController(auth, null!, null!),
                _ => new ApproverController(auth, null!)
            };
            var passwordOption = role == "admin" ? 8 : role == "approver" ? 5 : 3;
            var savedInput = Console.In;
            var savedOutput = Console.Out;
            using var input = new StringReader($"{passwordOption}\n{original}\n{replacement}\nMismatch\n{passwordOption}\n{original}\n{replacement}\n{replacement}\n0\n");
            using var output = new StringWriter();
            try
            {
                Console.SetIn(input);
                Console.SetOut(output);
                await controller.RunAsync(session);
            }
            finally
            {
                Console.SetIn(savedInput);
                Console.SetOut(savedOutput);
            }
            check(output.ToString().Contains($"{passwordOption} Change password") &&
                output.ToString().Contains("New passwords do not match") &&
                output.ToString().Contains("Password changed."), $"{role}: main-menu password change validates confirmation");
            check((await auth.LoginAsync(email, replacement)).UserId == id, $"{role}: new password signs in successfully");
            rejected = false;
            try { await auth.LoginAsync(email, original); }
            catch (UnauthorizedAccessException) { rejected = true; }
            check(rejected, $"{role}: old password no longer signs in");
            await using var db = database.Open();
            var stored = await db.Users.SingleAsync(user => user.Id == id);
            var audits = await db.AuditLogs.Where(row => row.UserId == id && row.Description == "Password changed").ToListAsync();
            check(stored.PasswordHash != original && stored.PasswordHash != replacement && audits.Count == 1,
                $"{role}: password hash and audit persist only for the successful change");
            await employees.DeleteEmployeeAsync(admin, id);
            rejected = false;
            try { await auth.ChangePasswordAsync(session, replacement, original); }
            catch (UnauthorizedAccessException) { rejected = true; }
            check(rejected, $"{role}: disabled account cannot change its password");
        }
    }
}
