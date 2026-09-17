# Copying and running the console project at TCS

This guide assumes TCS means another workstation where you can run .NET and connect to PostgreSQL. If you mean an online judge accepting only one C# file, this application will not run unchanged there: it is a multi-file project with NuGet dependencies and a PostgreSQL database.

## 1. What to copy

Use artifacts/CriticalCare.Console.Source.zip created by scripts/package-console.py. Extract the entire archive into one folder, keeping all relative paths. Do not copy only ConsoleApp: it contains the UI, while startup, services, entities and database mappings are outside it.

The archive contains the current console source, migrations, tests and placeholder configuration. It excludes the old LegacyWeb application, local backups, Git metadata, compiled bin/obj folders and local database credentials. The exact file list is at the end of this guide.

## 2. What the destination computer needs

- .NET 8 SDK, matching global.json. Check with `dotnet --list-sdks`.
- A reachable PostgreSQL server and an existing empty database for this application.
- A PostgreSQL account allowed to create tables and apply migrations in that database.
- Access to restore the NuGet packages in EquipmentManagementBackend.csproj, or an approved offline package source/cache.

This ZIP contains code, not a PostgreSQL installation, database backup or .NET installer. If software/network installation is restricted on the destination, arrange these prerequisites first.

## 3. Configure the destination database

Create an empty database using your PostgreSQL administration tool. The package uses the example name criticalcare_console. In the extracted appsettings.json replace YOUR_PASSWORD and adjust Host, Port, Database and Username for the destination. The package deliberately does not carry this computer's password.

Alternatively set ConnectionStrings__DefaultConnection in the launching PowerShell session. That environment setting takes precedence over appsettings.json. Do not publish the real password in a shared document.

## 4. Run from the extracted folder

Open a terminal in the folder containing Program.cs and EquipmentManagementBackend.csproj. Then run:

```powershell
dotnet run --project EquipmentManagementBackend.csproj -c Release
```

That command restores packages, builds the project and starts it. Startup applies included migrations to the configured database. Do not run from ConsoleApp/ and do not start the LegacyWeb project.

On a new database, follow the first-administrator setup prompt. Create a new admin email/password, sign in, and create employees. Existing seeded/sample identities do not supply usable default login passwords. Configure active technician and approver accounts before adding managed equipment. Passwords must be 10-128 characters.

## 5. Verify the copy

```powershell
dotnet build EquipmentManagementBackend.csproj -c Release
dotnet run --project EquipmentManagementBackend.csproj -c Release -- --self-test
```

Self-tests create and drop a randomly named isolated PostgreSQL schema. The account needs schema creation permission for this check. The current application has a console test runner; dotnet test alone does not run these checks.

If connecting fails, verify the database exists, server is running/reachable, port and credentials are correct. If package restore fails, check the permitted NuGet network/offline source. If the SDK cannot be found, check global.json against installed .NET 8 SDKs.

## 6. Existing data and offline use

Copying source code does not copy employees, passwords, equipment or ticket history. To keep existing data, arrange a separate PostgreSQL backup/restore through the database owner. Otherwise use a new empty database and set up accounts through the application.

A computer with neither NuGet access nor a package cache cannot restore this source ZIP in one command. For that environment, prepare a published application on a connected machine for the destination OS/architecture, or provision an approved offline package feed. PostgreSQL connectivity is still required.

## 7. Exact files included

Paths below are relative to the extracted project root. appsettings.json is generated with placeholder credentials. SQL files and documentation are included for development/reference; running the application uses the EF migrations automatically. Tests/Console is included because the root project compiles its built-in self-test runner.

```text
.config/dotnet-tools.json
.editorconfig
AI_CONTEXT_README.md
appsettings.json
ConsoleApp/README.md
ConsoleApp/Views/Admin/AuditView.cs
ConsoleApp/Views/Admin/DashboardView.cs
ConsoleApp/Views/Admin/EmployeeView.cs
ConsoleApp/Views/Admin/EquipmentAssignmentView.cs
ConsoleApp/Views/Admin/ExceptionDashboardView.cs
ConsoleApp/Views/Admin/ReferenceView.cs
ConsoleApp/Views/Approver/ApproverTicketView.cs
ConsoleApp/Views/Shared/BackupView.cs
ConsoleApp/Views/Shared/ConsoleApplicationLogger.cs
ConsoleApp/Views/Shared/ConsoleInput.cs
ConsoleApp/Views/Shared/ConsoleView.cs
ConsoleApp/Views/Shared/EquipmentDetailsView.cs
ConsoleApp/Views/Shared/EquipmentListView.cs
ConsoleApp/Views/Shared/TicketView.cs
ConsoleApp/Views/Staff/StaffTicketView.cs
ConsoleApp/Views/Technician/TechnicianTicketView.cs
Controllers/AdminController.cs
Controllers/AdminTicketController.cs
Controllers/ApplicationController.cs
Controllers/ApproverController.cs
Controllers/BackupController.cs
Controllers/EmployeeController.cs
Controllers/EquipmentController.cs
Controllers/ExceptionDashboardController.cs
Controllers/IncidentController.cs
Controllers/MenuController.cs
Controllers/StaffController.cs
Controllers/TechnicianController.cs
Data/AppDbContext.cs
Data/baseline-existing.sql
Data/DbSeeder.cs
Data/DesignTimeFactory.cs
Data/sample-equipment.sql
docs/TCS_TRANSFER_GUIDE.md
DTOs/AllocationView.cs
DTOs/AssignEquipmentUserRequest.cs
DTOs/BackupActorRequest.cs
DTOs/BackupDetails.cs
DTOs/BackupQuery.cs
DTOs/CancelBackupRequest.cs
DTOs/CreateBackupRequestRequest.cs
DTOs/CreateEquipmentRequest.cs
DTOs/CreateTicketRequest.cs
DTOs/DashboardReport.cs
DTOs/EquipmentAssignees.cs
DTOs/EquipmentQuery.cs
DTOs/EquipmentView.cs
DTOs/ExceptionDashboardReport.cs
DTOs/ExceptionRecord.cs
DTOs/ManagedEquipmentDetails.cs
DTOs/PagedResult.cs
DTOs/PageQuery.cs
DTOs/ReferenceData.cs
DTOs/SaveEquipmentInput.cs
DTOs/TicketDetails.cs
DTOs/UpdateEquipmentRequest.cs
DTOs/UpdateTicketStatusRequest.cs
EquipmentManagementBackend.csproj
global.json
GlobalUsings.cs
Infrastructure/InputValidation.cs
Infrastructure/ServiceException.cs
Interfaces/Controllers/IRoleController.cs
Interfaces/Repositories/IApplicationRepository.cs
Interfaces/Repositories/IRepository.cs
Interfaces/Repositories/IUnitOfWork.cs
Interfaces/Services/IApplicationLogger.cs
Interfaces/Services/IAuthenticationService.cs
Interfaces/Services/IBackupReservationService.cs
Interfaces/Services/IBackupsService.cs
Interfaces/Services/IEmployeeService.cs
Interfaces/Services/IEquipmentManagementService.cs
Interfaces/Services/IEquipmentService.cs
Interfaces/Services/IEquipmentTypeService.cs
Interfaces/Services/IExceptionDashboardService.cs
Interfaces/Services/ILocationService.cs
Interfaces/Services/IMaintenanceService.cs
Interfaces/Services/IReportsService.cs
Interfaces/Services/ISchedulingService.cs
Migrations/20260914160344_InitialSchema.cs
Migrations/20260914160344_InitialSchema.Designer.cs
Migrations/20260914161134_BackupReservationWorkflow.cs
Migrations/20260914161134_BackupReservationWorkflow.Designer.cs
Migrations/20260916143310_ConsoleComplianceWorkflow.cs
Migrations/20260916143310_ConsoleComplianceWorkflow.Designer.cs
Migrations/AppDbContextModelSnapshot.cs
Models/AuditLog.cs
Models/Authentication/Session.cs
Models/BackupAllocation.cs
Models/BackupEscalation.cs
Models/BackupRequest.cs
Models/Enums/AuditAction.cs
Models/Enums/AuditResult.cs
Models/Enums/BackupAllocationStatus.cs
Models/Enums/BackupRequestPriority.cs
Models/Enums/BackupRequestStatus.cs
Models/Enums/EquipmentAvailability.cs
Models/Enums/MaintenanceFrequency.cs
Models/Enums/NotificationType.cs
Models/Enums/TicketDocumentType.cs
Models/Enums/TicketHistoryAction.cs
Models/Enums/TicketPriority.cs
Models/Enums/TicketStatus.cs
Models/Enums/TicketType.cs
Models/Enums/WardType.cs
Models/Equipment.cs
Models/EquipmentApprover.cs
Models/EquipmentCalibrationRule.cs
Models/EquipmentTechnician.cs
Models/EquipmentType.cs
Models/Location.cs
Models/MaintenanceSchedule.cs
Models/Notification.cs
Models/Ticket.cs
Models/TicketCalibrationResult.cs
Models/TicketComplianceItem.cs
Models/TicketComplianceVerification.cs
Models/TicketDocument.cs
Models/TicketHistory.cs
Models/User.cs
Program.cs
README.md
Repositories/Database.cs
Repositories/EfRepository.cs
Repositories/EfUnitOfWork.cs
Services/Administration/EmployeeService.cs
Services/Administration/EquipmentManagementService.cs
Services/Administration/EquipmentTypeService.cs
Services/Administration/LocationService.cs
Services/Authentication/AuthenticationService.cs
Services/Backups/BackupsService.cs
Services/Inventory/BackupReservationService.cs
Services/Inventory/EquipmentService.cs
Services/InventoryTransaction.cs
Services/Maintenance/MaintenanceService.cs
Services/Maintenance/Schedule.cs
Services/Maintenance/SchedulingService.cs
Services/Reports/ExceptionDashboardService.cs
Services/Reports/ReportsService.cs
Tests/Console/AdminMenuTests.cs
Tests/Console/BackupFlowTests.cs
Tests/Console/EquipmentCreationTests.cs
Tests/Console/ExceptionDashboardTests.cs
Tests/Console/PasswordChangeTests.cs
Tests/Console/SelfTests.cs
Tests/Console/TechnicianDashboardTests.cs
```
