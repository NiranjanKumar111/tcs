using System.Text.Json;
using CriticalCare.ConsoleApp;
using EquipmentManagementBackend.Data;
using Microsoft.EntityFrameworkCore;

try
{
    var settingsIndex = Array.IndexOf(args, "--settings");
    var path = settingsIndex >= 0 && settingsIndex + 1 < args.Length ? args[settingsIndex + 1] : Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    if (string.IsNullOrWhiteSpace(connection))
    {
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        connection = json.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
    }

    if (string.IsNullOrWhiteSpace(connection))
    {
        throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection.");
    }

    if (args.Contains("--self-test"))
    {
        await SelfTests.RunAsync(connection);
        return;
    }

    var database = new Database(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    await using (var db = database.Open())
    {
        await db.Database.MigrateAsync();
    }

    if (args.Contains("--migrate-only"))
    {
        Console.WriteLine("Console schema migrations applied successfully.");
        return;
    }

    var auth = new AuthenticationService(database);
    var logger = new ConsoleApplicationLogger();
    var backups = new BackupsService(database, auth, logger);
    using var shutdown = new CancellationTokenSource();
    var scheduling = new SchedulingService(database, auth, logger);
    var expiry = backups.RunExpiryAsync(shutdown.Token);
    var scheduler = scheduling.RunAsync(shutdown.Token);
    try
    {
        await using (var seed = database.Open())
        {
            await DbSeeder.SeedAsync(seed);
        }

        var employees = new EmployeeService(database, auth);
        var equipmentManagement = new EquipmentManagementService(database, auth);
        var maintenance = new MaintenanceService(database, auth);
        var reports = new ReportsService(database, auth);
        var backupScreen = new BackupController(auth, backups, reports);
        var exceptionDashboard = new ExceptionDashboardController(new ExceptionDashboardService(database, auth));
        var ui = new ApplicationController(auth, new IRoleController[] {
                new AdminController(
                    auth,
                    reports,
                    new EmployeeController(auth, employees),
                    new EquipmentController(auth, equipmentManagement, reports),
                    new AdminTicketController(auth, maintenance, employees),
                    backupScreen,
                    exceptionDashboard),
                new StaffController(auth, new IncidentController(auth, maintenance, reports), backupScreen),
                new TechnicianController(auth, maintenance, reports),
                new ApproverController(auth, maintenance)
            });
        await ui.RunAsync();
    }
    finally
    {
        shutdown.Cancel();
        await Task.WhenAll(expiry, scheduler);
    }
}
catch (Exception error)
{
    Console.Error.WriteLine("Application could not complete: " + error.Message);
    Environment.ExitCode = 1;
}
