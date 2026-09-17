# Equipment Management ? console application

Run from this repository root:

```powershell
dotnet run --project EquipmentManagementBackend.csproj -c Release
```

The root `Program.cs` configures the database, services, background tasks and console menus.

```text
Program.cs                 Application entry point
ConsoleApp/Views/          Console input and display formatting
Controllers/               Menu actions and workflow coordination
Services/                  Authentication, administration, inventory, backups,
                           maintenance, scheduling and reports
Interfaces/                Service and repository contracts
Repositories/              EF Core repository and unit-of-work implementations
Infrastructure/            Domain exceptions
Models/ and DTOs/          One entity or data contract per file
Data/ and Migrations/      PostgreSQL configuration and schema migrations
Tests/Console/             Console workflow self-tests
LegacyWeb/                 Earlier Web API, separately runnable
```

Configuration comes from `appsettings.json`, `--settings PATH`, or
`ConnectionStrings__DefaultConnection`. Startup applies pending migrations.
See [console workflow documentation](ConsoleApp/README.md) for roles and usage.

```powershell
dotnet build EquipmentManagementBackend.csproj -c Release
dotnet run --project EquipmentManagementBackend.csproj -c Release -- --self-test
dotnet run --project EquipmentManagementBackend.csproj -c Release -- --migrate-only
```

Self-tests require PostgreSQL and create and remove an isolated test schema.
The original API is preserved in `LegacyWeb/EquipmentManagementBackend.Web.csproj`;
its integration runner remains `Tests/EquipmentManagementBackend.IntegrationTests.csproj`.
The API does not enforce the console authentication policies.

## How the layers work

A console action follows this flow:

```text
Console view -> Controller -> Service interface -> Service -> Repository -> PostgreSQL
```

Controllers collect input through console views, call services and pass returned data to views.
Services enforce permissions and business rules; they do not print screens. Repositories handle
persistence. The root `Program.cs` creates and connects these objects.

| Component | Contract | Implementation |
| --- | --- | --- |
| Login and permissions | IAuthenticationService | AuthenticationService |
| Employees | IEmployeeService | EmployeeService |
| Locations | ILocationService | LocationService |
| Equipment types | IEquipmentTypeService | EquipmentTypeService |
| Equipment administration and schedules | IEquipmentManagementService | EquipmentManagementService |
| Inventory operations | IEquipmentService | EquipmentService |
| Backup request access | IBackupsService | BackupsService |
| Reservation transactions | IBackupReservationService | BackupReservationService |
| Maintenance workflow and evidence | IMaintenanceService | MaintenanceService |
| Automatic scheduling | ISchedulingService | SchedulingService |
| Report queries | IReportsService | ReportsService |

Start with `Controllers/TechnicianController.cs` and follow a named action to
`Interfaces/Services/IMaintenanceService.cs`, then `Services/Maintenance/MaintenanceService.cs`.
`ConsoleApp/Views/Shared/TicketView.cs` displays the resulting ticket data.

`IRepository<T>`, `IApplicationRepository`, and `IUnitOfWork` each have their own file.
Calendar calculations, validation and transaction locking are small shared helpers rather than
independent business components. The database schema and migrations are unchanged by this refactor.

## Admin navigation

The Admin menu is numbered 1?7: Dashboard KPI, Employee, Equipment, Ticket,
Backup allocation, Exception dashboard and Audit logs. Employee and Equipment have
Add/Delete/Edit submenus. Ticket has Create, Reassign technician and All history/details.
See [the complete menu](ConsoleApp/README.md#admin-menu).
Audit logs show the full retained history using the existing table.

## AI handoff and development guide

- [AI project context](AI_CONTEXT_README.md): current architecture, menus, business rules, commands and test entry points.
- [Feature change guide](docs/FEATURE_CHANGE_GUIDE.md): plain-language recipes with exact files and methods to edit.
- [Word copy of the change guide](docs/FEATURE_CHANGE_GUIDE.docx).
- [Browser-readable change guide](docs/FEATURE_CHANGE_GUIDE.html).

Regenerate the Word/HTML copies after editing the guide with `python scripts/export-feature-guide.py`.

## Role-based view folders

Views are organized under `ConsoleApp/Views/`:

- `Admin/`: employee, dashboard, audit, exception, reference and equipment-assignment views.
- `Staff/StaffTicketView.cs`: staff ticket creation inputs and confirmation.
- `Technician/TechnicianTicketView.cs`: technician response, checklist and calibration inputs.
- `Approver/ApproverTicketView.cs`: accept/reject inputs and required rejection reason.
- `Shared/`: reusable ticket, equipment and backup displays, console input/menu helpers and logging.

Controllers still coordinate workflows; services still enforce permissions. Shared views do not grant access.
View namespaces match these folders: `CriticalCare.ConsoleApp.Views.<Folder>`.
