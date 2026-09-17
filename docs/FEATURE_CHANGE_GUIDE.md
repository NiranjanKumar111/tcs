# How to change this console application

A practical guide in simple words. Paths are relative to the folder containing Program.cs and EquipmentManagementBackend.csproj. Examples are instructions for future development; they are not already-added features.

## 1. Understand the five places involved

A screen is not stored in one file. Its menu, printed text, rules and stored data have different owners.

1. Controller: chooses what happens after an option is selected. Look in Controllers/.
2. View: prints information and reads reusable input. Look in ConsoleApp/Views/.
3. Service: checks who can perform the action, validates it and changes or reads data. Look in Services/.
4. Model and database configuration: describe stored data. Look in Models/ and Data/AppDbContext.cs.
5. Program.cs: creates services/controllers and connects them.

An interface is the list of operations a service promises to provide. For example, IMaintenanceService declares ReviewAsync, and MaintenanceService contains the actual review code. A DTO is a small object used to carry inputs or results. A migration describes how an existing database should change.

Start at the screen's controller. Follow its service call. Do not start by changing the database for a simple label change.

## 2. Find the exact component

| What you want to change | Start here | Related rules/output |
| --- | --- | --- |
| Login or first administrator setup | Controllers/ApplicationController.cs | Services/Authentication/AuthenticationService.cs |
| Password change prompts | Controllers/MenuController.cs: ChangePasswordAsync | AuthenticationService.ChangePasswordAsync; IAuthenticationService |
| Admin main options | Controllers/AdminController.cs: RunAsync | Program.cs for new dependencies |
| Employee menu/actions | Controllers/EmployeeController.cs | Services/Administration/EmployeeService.cs; ConsoleApp/Views/Admin/EmployeeView.cs |
| Equipment add/edit/view | Controllers/EquipmentController.cs | Services/Administration/EquipmentManagementService.cs; EquipmentListView.cs; EquipmentDetailsView.cs |
| Name-based employee selection | ConsoleApp/Views/Admin/EquipmentAssignmentView.cs | EmployeeService / EquipmentManagementService supply eligible users |
| Admin ticket creation/reassignment | Controllers/AdminTicketController.cs | Services/Maintenance/MaintenanceService.cs |
| Staff main menu | Controllers/StaffController.cs | IncidentController.cs and BackupController.cs |
| Faulty equipment reporting | Controllers/IncidentController.cs | MaintenanceService.CreateAsync |
| Technician menus | Controllers/TechnicianController.cs | TicketsMenuAsync, EquipmentMenuAsync, UpdateTicketAsync |
| Technician equipment permission | Services/Reports/ReportsService.cs | EquipmentAsync, TechnicianEquipmentAsync; Services/Inventory/EquipmentService.cs |
| Ticket read permissions | Services/Maintenance/MaintenanceService.cs | GetVisibleTickets, ListAsync, DetailAsync |
| Approver menu and review prompts | Controllers/ApproverController.cs | ReviewEvidenceAndApproveRejectAsync; MaintenanceService.ReviewAsync |
| Ticket list/detail formatting | ConsoleApp/Views/Shared/TicketView.cs | DTOs/TicketDetails.cs |
| Backup prompts/history | Controllers/BackupController.cs | ConsoleApp/Views/Shared/BackupView.cs |
| Backup access and ownership | Services/Backups/BackupsService.cs | Interfaces/Services/IBackupsService.cs |
| Reservation selection/expiry/pickup | Services/Inventory/BackupReservationService.cs | Services/InventoryTransaction.cs |
| Automatic due-ticket generation | Services/Maintenance/SchedulingService.cs | Services/Maintenance/Schedule.cs for date arithmetic |
| Dashboard figures | Services/Reports/ReportsService.cs | ConsoleApp/Views/Admin/DashboardView.cs; DTOs/DashboardReport.cs |
| Exception screens | Controllers/ExceptionDashboardController.cs | Services/Reports/ExceptionDashboardService.cs; ExceptionDashboardView.cs |
| Audit display | ConsoleApp/Views/Admin/AuditView.cs | ReportsService.AuditAsync; Repositories/EfUnitOfWork.cs |
| All menu layout/input | ConsoleApp/Views/Shared/ConsoleView.cs | ConsoleInput.cs; Controllers/MenuController.cs |

Names such as EquipmentDetailsView.cs in the table refer to files under ConsoleApp/Views/. Service contracts are under Interfaces/Services/.

## 3. Change a menu label or order

Open the controller's RunAsync method, or its named submenu method. A typical entry looks like this:

```csharp
["1"] = ("View assigned tickets", () => TicketLogsAsync(session)),
["2"] = ("Update ticket", () => UpdateTicketAsync(session))
```

The number is the input the user types. The text is what the console displays. The function on the right is the action executed. Change only the text to rename an option. To reorder actions, move entries and renumber them 1, 2, 3 without gaps. Keep 0 for Back/logout; MenuController handles it separately.

Update both displayed numbers and tests that type those numbers. The dictionary is displayed in its insertion order by ConsoleView.ReadMenuChoice. Changing its labels does not change service permissions or database data.

ExceptionDashboardController is a special case: it uses a switch and ExceptionDashboardView.ReadOption rather than the shared dictionary. Change both the printed options and switch cases together. BackupView also contains the immediate pickup/cancel choices.

## 4. Add a screen option using an existing service

Example idea: add a read-only list to a menu.

1. Find a service method that already returns the needed information.
2. Add a private async controller method that calls it using the logged-in session.
3. Pass its result to the appropriate view.
4. Add a numbered dictionary entry calling the controller method.
5. If the controller needs a new service, add a constructor parameter and field; update Program.cs and all test constructors.
6. Try the option with an allowed account and an account that must not have access.

For example, TechnicianController.TicketLogsAsync calls maintenance.ListAsync(session) and TicketView.ShowTickets. The service applies ownership filtering. Reuse that service rule instead of loading all tickets and hiding some on the screen.

Do not put EF queries in a view. Do not create a database context in every controller action when an application service should own the operation.

## 5. Remove a feature from a screen

There are three different meanings of remove:

- Hide the entry: remove its dictionary entry and renumber the remaining menu. The service remains usable elsewhere.
- Remove the workflow: remove the entry and controller handler, then check whether other code uses its service operation before deleting it.
- Remove stored data: requires a separate database design decision and usually a migration. Removing a menu does not require dropping a table.

Use searches before deleting a method:

```powershell
rg -n 'MethodName' Controllers Services Interfaces Tests Program.cs
```

If no remaining feature needs the operation, remove its interface declaration, implementation, unused DTOs and constructor dependency as appropriate. Update Program.cs and tests. Keep historical ticket/review/allocation records unless deletion of that data is explicitly intended.

Hiding a restricted action is not a security change. To disallow it for a role, update the service authorization too. Do not weaken other roles' access by accident.

## 6. Change the technician Update ticket flow

Open Controllers/TechnicianController.cs and find UpdateTicketAsync. The current sequence is:

1. Read the ticket ID once.
2. Load details through MaintenanceService.DetailAsync, which checks ownership.
3. Show the ticket and previous response/rejection history using TicketView.ShowDetails.
4. Check whether its status permits work.
5. Read the response and call ProgressAsync.
6. Add/update checklist evidence. Calibration tickets also collect measurements.
7. Ask whether to send for approval. No keeps saved work for later.
8. Read completion date and call SubmitAsync.

To insert a new prompt, decide whether it is only display text, an input to an existing service, or a new stored field. Place the prompt at the correct step and pass its value to a service. Keep the ticket ID already captured; do not ask for it again at every step.

Each successful service write increments the version. The controller reloads details between steps so the next operation uses the new version. If another session changes the ticket, stale-write protection can reject the action. Do not remove version checking just to avoid an error.

Work is saved step by step, not as one transaction covering human input. Exiting or failing later can leave an already-saved response/evidence in place. Never hold a database transaction open while waiting for keyboard input.

Checklist/measurement prompts are required by the current service rules. If you remove them from the UI without changing the business requirement, submission will fail when required evidence is missing. Do not invent passing evidence automatically.

## 7. Change approval or rejection

Screen: Controllers/ApproverController.cs, ReviewEvidenceAndApproveRejectAsync.
Rules: Services/Maintenance/MaintenanceService.cs, ReviewAsync.
Output: ConsoleApp/Views/Shared/TicketView.cs, ShowDetails.

The screen loads a pending ticket, displays the technician response, then asks Accept or Reject. A blank rejection reason is prompted again. ReviewAsync also validates notes, approver ownership, ticket status and version, then saves the review and history.

Rejecting keeps AssignedTo unchanged and sets rejected. That technician can update and resubmit. Accepting sets approved. CloseAsync separately changes approved to completed and updates equipment due dates. If you want acceptance to also close tickets, that is a business-rule change involving both operations and schedule tests, not just a menu rename.

## 8. Add a new business operation

Suppose a new feature must save a ticket comment.

1. Decide who can add it, which ticket states permit it, what must be stored, and what the user sees afterward.
2. Add a method to Interfaces/Services/IMaintenanceService.cs.
3. Implement it in Services/Maintenance/MaintenanceService.cs.
4. Open a unit of work through database.Open(). For mutations use the existing transaction pattern where appropriate.
5. Check the session role through auth.RequireAsync, then ticket ownership/state/version.
6. Validate text length and required values in the service, even if the screen validates too.
7. Save the change, increment the version where it changes the ticket, and record history/audit as needed.
8. Commit the transaction only after the related records have saved.
9. Add the controller prompt and view output.
10. Test allowed behavior, rejected behavior and persistence.

Copy the structure of a nearby action such as ChecklistAsync, but adapt the rule rather than blindly copying its fields. DTOs are useful when an operation has many related arguments. Most existing services accept Session so the caller cannot choose another user's identity.

## 9. Add a database field or a whole entity

For a field on an existing entity, such as an optional equipment note:

1. Add the property to Models/Equipment.cs.
2. Configure maximum length, nullability and mapping in Data/AppDbContext.cs as needed.
3. Add the input/result property to relevant DTOs, such as SaveEquipmentInput or an equipment result.
4. Validate and assign it in EquipmentManagementService.SaveEquipmentAsync.
5. Collect/display it in EquipmentController and EquipmentDetailsView.
6. Create and review a migration, including what happens to existing rows.
7. Apply it to a development database and run relevant checks.

For an entirely new entity, also expose its DbSet in AppDbContext, its IRepository property in Interfaces/Repositories/IUnitOfWork.cs, and its adapter property in Repositories/EfUnitOfWork.cs. Add foreign keys/indexes in the context configuration. A field added to an existing entity usually does not require a new repository property.

With a compatible EF command-line tool already installed:

```powershell
dotnet ef migrations add AddEquipmentNote --project EquipmentManagementBackend.csproj --configuration Release
dotnet run --project EquipmentManagementBackend.csproj -c Release -- --migrate-only
```

Use a descriptive migration name matching your actual change. Review generated changes before applying. A required column on existing rows needs a sensible backfill or staged migration. Do not invent historical maintenance completion dates. Do not rewrite previously applied migrations to make a local database appear correct.

Data/DesignTimeFactory.cs supplies configuration to EF tooling. Normal runtime configuration is in Program.cs. Both need the intended database connection.

## 10. Change filtering, tables and access

For a new printed ticket column, edit TicketView.ShowTickets or ShowDetails. If the returned Ticket already contains the value, a migration is unnecessary. If extra joined information is needed, extend the result DTO and service query first.

For technician equipment lists, ReportsService.EquipmentAsync supplies the logged-in technician ID to the inventory service. EquipmentService.ListAsync applies that filter before Count/Skip/Take. Keep this order; filtering only a returned page gives incorrect counts and can expose other users' data.

For single equipment lookup, ReportsService.TechnicianEquipmentAsync checks for an assigned ticket before returning details. Test both the list and direct-ID route. Removing a ticket assignment must remove access when no other qualifying assignment remains.

## 11. Change background behavior

SchedulingService.GenerateCoreAsync decides when scheduled tickets are created. Schedule.Next calculates the next date for each frequency. Program.cs starts SchedulingService.RunAsync.

BackupReservationService handles inventory selection and expiration rules; BackupsService runs the expiry loop and exposes authorized operations. The running application must remain open for its periodic loops to execute. Actions also enforce relevant expiration rules.

Change the business rule in the service, then test its boundary. Examples: exactly two days before due versus three days; pickup before expiration versus at expiration; two competing requests for one item. Keep the shared InventoryTransaction lock around dependent reads and writes.

## 12. Verify and diagnose your change

```powershell
dotnet build EquipmentManagementBackend.csproj -c Release
dotnet run --project EquipmentManagementBackend.csproj -c Release -- --self-test
```

The build catches missing methods, wrong parameters and constructor mismatches. Self-tests exercise actual PostgreSQL behavior in an isolated temporary schema. They are run through --self-test, not merely dotnet test. Tests/Console/SelfTests.cs calls the focused test classes.

Use AdminMenuTests for admin navigation, EquipmentCreationTests for equipment/scheduling, TechnicianDashboardTests for technician ownership and review flow, PasswordChangeTests for all role password menus, BackupFlowTests for backup interaction, and ExceptionDashboardTests for exception reporting.

Menu tests feed a StringReader into Console.In. Each line is a typed answer. If you add a prompt, update that sequence in the same order; otherwise later answers may be interpreted as the wrong input. Always restore Console.In and Console.Out in finally blocks.

If the running app locks the build executable, build into a separate output folder rather than stopping someone else's session:

```powershell
dotnet build EquipmentManagementBackend.csproj -c Release -o artifacts/equipment-verification --no-restore
dotnet artifacts/equipment-verification/EquipmentManagementBackend.dll --self-test
```

Common diagnoses:

- New option missing: wrong controller, stale executable, or option added to a submenu instead of the main menu.
- Compile error after adding a dependency: update Program.cs and test constructor calls.
- No database change: verify SaveChangesAsync and transaction commit, and confirm the configured connection.
- Update refused: read role, ownership, state and stale-version checks before changing them.
- Submission refused: inspect work report, checklist, calibration readings and approver assignment.
- Wrong screen changed: check whether you edited LegacyWeb instead of the root console project.

## 13. A repeatable checklist for your next change

Write the desired user journey. Identify controller, view and service. Decide whether stored data changes. Implement the smallest coherent change. Preserve permissions and transaction rules. Update menu numbering and input tests. Build and run the relevant PostgreSQL checks. Update AI_CONTEXT_README.md and ConsoleApp/README.md when behavior changes. Report which files changed and what you actually verified.

To hand work to another AI, provide AI_CONTEXT_README.md and the current repository or relevant source files. State the exact desired change. The handoff README explains the project, but it cannot replace source inspection or current test results.

## Role-based view folders

Views are organized under `ConsoleApp/Views/`:

- `Admin/`: employee, dashboard, audit, exception, reference and equipment-assignment views.
- `Staff/StaffTicketView.cs`: staff ticket creation inputs and confirmation.
- `Technician/TechnicianTicketView.cs`: technician response, checklist and calibration inputs.
- `Approver/ApproverTicketView.cs`: accept/reject inputs and required rejection reason.
- `Shared/`: reusable ticket, equipment and backup displays, console input/menu helpers and logging.

Controllers still coordinate workflows; services still enforce permissions. Shared views do not grant access.
View namespaces match these folders: `CriticalCare.ConsoleApp.Views.<Folder>`.
