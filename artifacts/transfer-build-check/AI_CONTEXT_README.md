# Equipment Management: AI project handoff

Snapshot: 17 September 2026. Read this together with the current source before modifying anything. This is project context, not authorization to perform every possible change described here. The user's current request defines the task.

## What this project does

A hospital equipment management console application for administrators, staff, technicians and approvers. It manages employees, equipment, preventive maintenance, calibration, faulty-equipment tickets, backup reservations, approval evidence and audit history.

The active application is the root .NET 8 console project, EquipmentManagementBackend.csproj. It uses EF Core and PostgreSQL directly. It does not start an HTTP server or browser. LegacyWeb/ contains a separate earlier API; changing that code generally does not change the active console application. Historical handoff documents may describe older APIs or ward-stock allocation that do not match current behavior.

## Start, build and verify

Run these commands from the repository root:

```powershell
dotnet build EquipmentManagementBackend.csproj -c Release
dotnet run --project EquipmentManagementBackend.csproj -c Release
dotnet run --project EquipmentManagementBackend.csproj -c Release -- --self-test
dotnet run --project EquipmentManagementBackend.csproj -c Release -- --migrate-only
```

Connection settings come from ConnectionStrings__DefaultConnection first, otherwise appsettings.json or the file passed after --settings. Do not paste credentials into documentation. Normal startup applies migrations, starts the reservation-expiry and scheduling loops, seeds reference data, and opens login/bootstrap.

If a running executable locks the normal build output:

```powershell
dotnet build EquipmentManagementBackend.csproj -c Release -o artifacts/equipment-verification --no-restore
dotnet artifacts/equipment-verification/EquipmentManagementBackend.dll --self-test
```

--no-restore assumes packages have already been restored. Console self-tests use the configured PostgreSQL connection, create a randomly named isolated schema and remove that schema afterward. The database account needs schema creation permission. Tests/EquipmentManagementBackend.IntegrationTests.csproj targets LegacyWeb, not the current console workflow. The last application verification before this documentation change passed 128 console PostgreSQL checks; rerun after code changes.

## Architecture and exact entry points

| Responsibility | Files |
| --- | --- |
| Startup, object construction, background tasks | Program.cs |
| Shared namespaces | GlobalUsings.cs |
| Login/bootstrap and role dispatch | Controllers/ApplicationController.cs |
| Menu loop, errors, shared password prompts | Controllers/MenuController.cs |
| Main role menus | Controllers/AdminController.cs, StaffController.cs, TechnicianController.cs, ApproverController.cs |
| Other menus | Controllers/EmployeeController.cs, EquipmentController.cs, AdminTicketController.cs, IncidentController.cs, BackupController.cs, ExceptionDashboardController.cs |
| Text input and printed output | ConsoleApp/Views/ |
| Service contracts | Interfaces/Services/ |
| Permission checks and business actions | Services/ |
| Repository contracts | Interfaces/Repositories/ |
| EF adapters and context factory | Repositories/Database.cs, EfUnitOfWork.cs, EfRepository.cs |
| Database entities and enums | Models/, Models/Enums/ |
| Inputs and returned data | DTOs/ |
| EF configuration and design-time factory | Data/AppDbContext.cs, Data/DesignTimeFactory.cs |
| Schema history | Migrations/ |
| Current application tests | Tests/Console/ |

Call path: console input -> controller -> service interface -> service -> unit of work -> PostgreSQL. Views format returned data. Program.cs manually constructs dependencies; it is not an automatic dependency-injection registration system. Most application interfaces/services use EquipmentManagementBackend.Application; views and console tests use CriticalCare.ConsoleApp.

## Current menus

All action menus use consecutive numbers and 0 for Back/logout.

Admin: 1 Dashboard KPI, 2 Employee, 3 Equipment, 4 Ticket, 5 Backup allocation, 6 Exception dashboard, 7 Audit logs, 8 Change password.

Employee: 1 Add, 2 Delete, 3 Edit, 4 View all employees, 5 View specific employee by ID.

Equipment administration: 1 Add, 2 Delete, 3 Edit, 4 View all equipment, 5 View specific equipment by ID/code.

Admin Ticket: 1 Create ticket, 2 View ticket history. Creation asks only for equipment ID and ticket type. Corrective/preventive priority is medium; calibration priority is low. Due date is the UTC registration date plus three days. Title and assignments are automatic; description is empty. Reassignment remains a service operation but is no longer a menu option.

Staff: 1 Ticket, 2 Backup allocation, 3 Change password. Ticket submenu: 1 Create ticket, 2 View ticket history. Staff can create corrective, preventive and calibration tickets using the same automatic defaults as admin; history remains restricted to their own tickets.

Backup menu for staff/admin: 1 Request backup allocation, 2 Allocation history. A successful reservation immediately offers pickup/confirm or cancel without re-entering IDs. History is scoped to the logged-in user's requests.

Technician: 1 Equipment, 2 Assigned tickets, 3 Change password. Equipment submenu: 1 View all assigned equipment, 2 View equipment by ID. Assigned tickets submenu: 1 View assigned tickets, 2 Update ticket. Update asks for the ID once, shows details, collects the technician response, checklist and calibration measurements when applicable, then offers submission to the assigned approver. Work can be saved without submitting. Versions are loaded automatically.

Approver: 1 Pending approvals, 2 Review pending ticket, 3 Ticket details/history, 4 Close approved ticket, 5 Change password. Pending approvals lists ticket IDs. Review displays the technician response and evidence, then 1 Accept, 2 Reject, 0 Back. Rejection requires a reason. Approval and closure remain separate steps.

## Business rules to preserve

- AuthenticationService checks active accounts and supported roles. Menu visibility is not authorization: services must enforce permissions too.
- Change password verifies the current password, validates a 10-128 character replacement, stores a salted PBKDF2 hash and audits the change. The old password stops working; an existing session remains open. Never print/hash-log actual password values.
- EquipmentManagementService generates EQ-number codes under the shared PostgreSQL inventory transaction lock. New equipment uses its UTC creation date as the schedule anchor. One frequency controls maintenance and calibration. Equipment plus one active technician and approver association are saved in the same transaction.
- Scheduler runs approximately every minute while the application is open. It generates tickets due within two UTC calendar days, including overdue work, uses equipment-assigned active employees, and prevents duplicate open cycles. Three-days-away work is excluded. Daily equipment can be eligible immediately.
- Technician ticket visibility requires AssignedTo equal to the current user. Equipment visibility requires at least one ticket currently assigned to that technician. An equipment-technician association alone does not grant this access. Completed tickets still count while assigned. Scope is applied before pagination.
- Technician equipment screens are read-only. MaintenanceService enforces ownership for ticket changes. ReportsService enforces technician equipment scoping, using the optional technician scope of the inventory EquipmentService for list queries.
- Workflow: open -> in_progress -> pending_approval -> approved -> completed. Rejection sends pending_approval -> rejected; the same assigned technician can revise and resubmit. VersionNumber increments on ticket changes and stale writes are rejected.
- Submission needs a work report and passing checklist. Calibration also needs passing measurements. An active assigned approver is required. Rejection reason is retained in review/history records. Closure advances the appropriate equipment schedule; acceptance alone does not close the ticket.
- Backup allocation currently selects matching available central-warehouse equipment. It excludes open-maintenance and occupied equipment. Reservation lasts 20 minutes; expiry, pickup, return, cancellation and escalation are enforced by services. Do not assume a ward-first selection policy from historical documents.
- Services/InventoryTransaction.cs uses PostgreSQL transaction-scoped advisory locking shared across processes. Preserve atomic check-and-write operations. EfUnitOfWork writes automatic audit metadata; explicit Database.Audit calls add meaningful actions.
- Employee/equipment deletion generally deactivates records and retains history. Existing assignment/open-work restrictions apply.

## Tests and edit guidance

Tests/Console/SelfTests.cs runs the workflow checks. Focused files include AdminMenuTests.cs, EquipmentCreationTests.cs, TechnicianDashboardTests.cs, PasswordChangeTests.cs, BackupFlowTests.cs and ExceptionDashboardTests.cs. These are an executable integration harness, not an xUnit console suite. The root project explicitly includes Tests/Console/**/*.cs.

When a menu changes, update redirected-input tests and documentation. When adding an interface method, implement it and update dependent constructors/call sites. When adding a persistent entity, update Models, DbContext mapping, IUnitOfWork, EfUnitOfWork and migrations. Avoid broad rewrites or overwriting unrelated working-tree changes.

Read docs/FEATURE_CHANGE_GUIDE.md for concrete editing recipes. Source code wins if this snapshot becomes stale. Do not treat speculative examples in the guide as requested features.

## Role-based view folders

Views are organized under `ConsoleApp/Views/`:

- `Admin/`: employee, dashboard, audit, exception, reference and equipment-assignment views.
- `Staff/StaffTicketView.cs`: staff ticket creation inputs and confirmation.
- `Technician/TechnicianTicketView.cs`: technician response, checklist and calibration inputs.
- `Approver/ApproverTicketView.cs`: accept/reject inputs and required rejection reason.
- `Shared/`: reusable ticket, equipment and backup displays, console input/menu helpers and logging.

Controllers still coordinate workflows; services still enforce permissions. Shared views do not grant access.
View namespaces match these folders: `CriticalCare.ConsoleApp.Views.<Folder>`.
