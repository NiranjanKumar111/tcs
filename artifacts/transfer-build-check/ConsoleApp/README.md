# Critical Care Compliance â€” .NET 8 console application

This program connects directly to PostgreSQL. It has no controllers, HTTP client, Swagger, API server,
or dependency on the old web project's executable. Its SDK is `Microsoft.NET.Sdk`, not the Web SDK.
The original web application is preserved separately in `LegacyWeb/`. The root `Program.cs` launches this console workflow.

## Run on this computer

From the repository root:

```powershell
dotnet run --project EquipmentManagementBackend.csproj --configuration Release
```

The existing `appsettings.json` connection is copied to the console build output. Override it using
`ConnectionStrings__DefaultConnection`, or supply `-- --settings C:\path\appsettings.json`.
An environment connection override takes precedence. The app applies pending migrations on startup.
No browser opens and no HTTP port is used.

On first launch, create a NEW admin email and password (10â€“128 characters). Bootstrap is permitted
only while no account has a password hash. It cannot claim an existing email. Password input is
hidden in an interactive terminal; only salted PBKDF2 hashes are stored. Then log in.
There are no default passwords. Existing sample accounts cannot log in until an administrator
sets a password and a supported role using Employee > Edit. The old `user` role should be changed to `staff`.

Every role's main menu has **Change password**: admin option **8**, approver option **5**, and staff/technician option **3**. Enter your current password, a new password
(10-128 characters), and the matching confirmation. The database stores a salted password hash;
after success, use the same email and the new password to sign in. The old password stops working.
Your current session remains open. Password changes are audited without recording password values.

Use Admin > 2 Employee > 1 Add to create at least one technician, approver, and staff account. Names/roles alone
do not log users in. Role checks run in services for every action, not just in the menu.
Inactive users lose access on the next action. Keep credentials for your administrator account.

## Roles and menus

| Role | Features |
| --- | --- |
| Admin | Dashboard KPI (1), Employee (2), Equipment (3), Ticket (4), Backup allocation (5), Exception dashboard (6), Audit logs (7) |
| Approver | Pending approvals (1), review pending ticket (2), ticket details/history (3), close an approved ticket (4), change password (5) |
| Technician | Equipment (1, view only), Assigned tickets (2, details/response/evidence/submit), Change password (3) |
| Staff | Availability (1), references (2), own service requests/history (3/4), raise corrective request (40), request backup (41), own allocations/history (42/43), reserve/pickup/return/cancel (44â€“47) |

Every role can view equipment and location/type reference data. Lists show IDs needed by the next
action. Ticket lists show the current VersionNumber: use it when changing a ticket. Refresh if a
concurrent user changed the version. Press 0 to log out and `exit` at the email prompt to quit.
At a failed input/action, the menu stays open. End-of-input exits instead of repeatedly prompting.

## Equipment and new schema

Location now has Building (150), Floor (100), Ward, optional Room (150) and Shelf (150).
An existing location's building/floor remain blank until edited; the migration does not invent data.
Existing wards default to OTHER. Existing location details can be maintained through the location service.

Equipment has MaintenanceFrequency, MaintenanceAnchorDate, LastMaintenanceDate,
NextMaintenanceDate, and equivalent calibration fields. Frequencies are `daily`, `weekly`,
`monthly`, `quarterly`, `half_yearly`, `yearly`, and `none` for unconfigured/not scheduled.
New/changed schedules are calculated from the last approved-and-closed work completion date,
or the creation-date anchor when no verified completion exists. Monthly/yearly calculations use
calendar arithmetic: January 31 + one month becomes the last valid day in February.

Existing equipment is migrated with frequency none and null maintenance/calibration dates.
Set its schedules using Admin > Equipment > Edit. A schedule is not proof maintenance was performed: initial
schedule anchors do not populate LastMaintenanceDate. Preventive closure updates maintenance;
calibration closure updates calibration; corrective repairs do not reset preventive due dates.
Closing an older ticket cannot move an already newer verified date backwards.

An item with an open maintenance ticket is excluded from backup allocation. An allocated item
must be returned/released before a technician starts work. Descriptive edits preserve the item;
moving, deactivating, or changing the type of occupied/open-maintenance equipment is rejected.

## Complete maintenance demonstration

1. Admin: Employee (2) > Add (1). Create active technician, approver and staff accounts.
2. Admin: Equipment (3) > Add (1) or Edit (3). Choose one shared maintenance/calibration frequency and select the technician and approver by name. Duplicate names require a matching employee ID. New equipment receives an automatic code and creation-date anchor; equipment and assignments are saved in one transaction.
3. Admin: Ticket (4) > Create ticket (1), choosing the equipment, due date, technician and approver.
   Blank assignments choose an active employee of the required role with the smallest open workload.
   If no employee exists, assignment remains empty and appears on the exception dashboard.
4. Technician: log in, open Assigned tickets (2), list tickets (1), then choose Update ticket (2) and enter its ID.
5. Technician: follow the update prompts to write a response and add checklist evidence. At least one passing checklist item is required; every
   recorded check must pass. The same description updates a prior item during active work.
6. Technician: confirm sending for approval in the update flow with completion date from ticket creation through today (UTC).
7. Approver: list pending (1), review pending ticket (2), inspect the technician response and evidence, then choose 1 Accept or 2 Reject.
   Only the assigned independent approver can review. Rejection records the reason; the assigned
   technician can restart/rework (30), correct evidence and resubmit. Submitted evidence is locked.
8. Approver: close an approved ticket (4). This records closure and updates the applicable next-due date.
9. Admin/approver: view updated compliance (Admin Dashboard KPI 1 / Approver 23). History and audit records remain available.

While the application is running, the scheduler checks every minute and generates preventive and
calibration tickets when due within two days (including overdue work), using the equipment's assigned
active technician and approver. Work due three days away is excluded; repeated checks avoid duplicate open tickets.

For a calibration ticket, also record parameter/unit/allowed minimum/maximum/measured result when prompted during Update ticket.
Out-of-range readings prevent submission/approval. Limits are entered with the measurement and
must be reviewed by the approver; a hospital-approved immutable template library is not implemented.
The screenshots show additional entity names but not their full definitions; the implemented evidence
tables are TicketComplianceItem, TicketCalibrationResult and TicketComplianceVerification.

Ticket transitions:

```text
open -> in_progress -> pending_approval -> approved -> completed
             ^               |
             +--- rejected <-+ (approver rejects with notes)
```

No role can jump directly from open to completed. Each mutation checks the actor, assignment,
allowed current state and latest version. Mutations/history/audit changes share a transaction.

## Backup demonstration

Staff menu 2 opens Backup allocation. Choose 1 to request equipment, then enter the ward type,
equipment type ID and bed number. The allocation result shows the selected equipment and pickup
location. Choose **1 Pick up / Confirm** to mark it in use, or **2 Cancel** to release it.
The app uses the displayed request/allocation automatically; no IDs need to be re-entered.
If stock is unavailable, it shows the escalation result instead of a pickup prompt.
Choose 2 in the backup menu to immediately display all requests made by the logged-in staff member, including their current statuses. No additional actions or submenu are shown.


Stock two matching items in the central warehouse. Log in as two different staff users and request
the same preferred item (41). Each receives a different available matching item. A further request
escalates only when all matching warehouse items are occupied or unavailable for maintenance.
Staff can only view/change their own requests; admins can manage all. Menu 43 shows the equipment,
allocation ID, expiry timestamp, seconds remaining and historical allocations/escalations.

Pickup (45) sets in_use. Return (46) releases the item. Reserve (44) retries an escalated or expired
request and resolves its open escalation when successful. Cancel (47) releases an unpicked reservation.
The 20-minute deadline is persisted in PostgreSQL; the console background loop checks every 15 seconds
while you are at a menu, including during input. Actions also check expiry. When the application is
closed the loop stops, then catches up when it runs again. There is no automatic waiting-list allocation.

Allocation and maintenance mutations use the same PostgreSQL transaction advisory lock. Partial
unique indexes prevent duplicate active allocations. Each background operation uses its own DbContext.

## Dashboard definitions

Dashboard counts are fresh database queries each time you choose the menu; they are not live-pushed
while waiting for input. It shows active equipment, open tickets, pending reviews, overdue tickets,
open escalations, and separate preventive/calibration verified-current percentages.

For each percentage, denominator = active equipment with that frequency configured; numerator =
those items with a recorded verified completion and next date today or later. No configured items
means N/A, not 100%. Overdue means due date before today's UTC date. A date due today is still current.
The exception dashboard lists overdue/unverified/unconfigured maintenance, critical/overdue/unassigned
tickets and unresolved backup escalations. These are operational tracking metrics, not certification
against a particular medical or legal standard.

## Database migration and tests

Migration `20260916143310_ConsoleComplianceWorkflow` adds location fields, password hashes,
schedule fields, ticket type/work timestamps, evidence/review tables and indexes. It preserves
existing rows; no passwords or verified maintenance dates are invented for existing users/equipment.

```powershell
dotnet run --project EquipmentManagementBackend.csproj -c Release -- --migrate-only
dotnet run --project EquipmentManagementBackend.csproj -c Release -- --self-test
```

Self-tests create a randomly named isolated schema in the configured PostgreSQL database and drop
only that schema afterward. They do not bootstrap or change the real accounts. The database user
must have schema creation permission. You can use an environment override to run on a test database.
Tests cover role denial, hashed login, calendar dates, evidence, rework/approval/closure, optimistic
versions, real concurrent backup allocation, expiry, isolation between staff and audit history.

The root console project owns the migration snapshot. To generate a migration:

```powershell
dotnet ef migrations add DescriptiveChange --project EquipmentManagementBackend.csproj --configuration Release
```

The root project compiles the application. `ConsoleApp/Views/` contains console input/output. `Controllers/` coordinates menu actions,
and `Interfaces/Controllers/` contains controller contracts. Business services live in `Services/`, service/repository contracts in
`Interfaces/`, persistence adapters in `Repositories/`, and domain errors in `Infrastructure/`.
Models, DTOs, database configuration and migrations remain in their root folders.
Self-tests live in `Tests/Console/SelfTests.cs` and run with the root project's `--self-test` option.

## Files to understand

| File/folder | Responsibility |
| --- | --- |
| ../Program.cs | Configuration, startup and application composition |
| ../Controllers/ | Named menu actions calling service interfaces |
| Views/ | Console input and separate ticket, equipment, backup and dashboard views |
| ../Interfaces/Services/ | One contract per business service |
| ../Services/Authentication/AuthenticationService.cs | Login, bootstrap and role validation |
| ../Services/Administration/ | Separate employee, location, equipment-type and equipment-management services |
| ../Services/Maintenance/MaintenanceService.cs | Ticket transitions, evidence, review and closure |
| ../Services/Maintenance/SchedulingService.cs | Automatic due-ticket generation |
| ../Services/Reports/ReportsService.cs | Typed dashboard, reference, audit and equipment data |
| ../Services/Backups/BackupsService.cs | Authenticated backup request operations |
| ../Services/Inventory/ | Inventory and reservation transaction services |
| ../Repositories/ | Database factory, repository and unit of work |
| ../Models/ and ../DTOs/ | Separate entity and result files |
| ../Tests/Console/SelfTests.cs | PostgreSQL workflow verification |

See the [root architecture guide](../README.md#how-the-layers-work) for the MVC-style flow
and the interface-to-service mapping. Run the application from the root project as shown above.

Employee login is local email/password authentication. It does not implement MFA, password reset
email, or directory integration. Keep the old unauthenticated web application stopped when using
this console-based role model; its older endpoints do not enforce these new console service policies.

## Admin exception dashboard

Admin option **6 ? Exception dashboard** contains only these choices plus Back:

1. **Overdue maintenance** ? unresolved preventive/calibration tickets due before today (UTC).
2. **Unresolved tickets** ? tickets whose status is neither completed nor cancelled.
3. **Backup exception counts** ? unresolved backup escalations and their request records.

Each choice shows its count and a table with Ticket ID, Type, Equipment name, Status and Priority.
Backup rows use the backup request number in the ID column. When no specific equipment was
requested, the equipment column shows the requested equipment type and "no equipment allocated".
Counts refresh whenever the submenu is displayed. Due-today tickets are not overdue.
This dashboard is restricted to admins in the service layer. Schedule dates without an associated
ticket are covered by the equipment compliance dashboard, not these ticket-based overdue counts.

## Admin menu

```text
1 Dashboard KPI
2 Employee
    1 Add
    2 Delete
    3 Edit
    4 View all employees
    5 View specific employee
3 Equipment
    1 Add
    2 Delete
    3 Edit
4 Ticket
    1 Create ticket
    2 Reassign ticket to new technician
    3 All ticket history / details
5 Backup allocation
    1 Request backup allocation
    2 Allocation history
6 Exception dashboard
    1 Overdue maintenance
    2 Unresolved tickets
    3 Backup exception counts
7 Audit logs
8 Change password
```

Every menu retains 0 for Back/logout. Employee/equipment deletion disables the record and
retains its history. An admin cannot delete their own account or an employee with open assigned
tickets. Equipment deletion retains the existing allocation and maintenance checks.
Employee viewing includes active and inactive accounts, displaying ID, name, email, role,
active status and whether login is configured. Option 5 looks up an employee by ID.
Both viewing options require administrator access and record an audit entry; password hashes are not displayed.
Ticket reassignment changes only the technician, preserves the approver and checks the latest version.
For reassignment, enter the ticket ID and choose a name from the displayed active technician list.
Duplicate names require a matching employee ID. The ticket version is loaded automatically and
incremented when saved. If another action changes the ticket during selection, retry reassignment
to load its updated version.
Ticket history/details displays all tickets with their evidence and history.
Admin backup allocation uses the same request, pickup/confirm, cancel and personal-history flow as Staff.

Audit logs display the full retained history, including successful/failed login, ticket generation,
backup requests/allocations, employee/equipment changes and record views. Records show timestamp,
actor, action, entity ID, result and description. The existing AuditLogs table supports this;
no migration or new table is required. Password values are not written to audit descriptions.
Previously stored entries remain unchanged; new login events have the explicit login action.

## Technician dashboard

The main menu contains **1 Equipment**, **2 Assigned tickets**, and **3 Change password**.
Equipment is view-only: list all equipment with tickets currently assigned to the signed-in technician,
or view one by equipment ID. Equipment links alone do not grant access. Ticket reassignment updates
visibility; completed tickets still count while assigned to that technician.

Assigned tickets contains **1 View assigned tickets** and **2 Update ticket**.
Update ticket asks for the ID once, displays details and any rejection reason, then collects the
technician response, checklist evidence, and calibration measurements when applicable. Confirm
sending for approval and enter the work completion date. Choosing not to send keeps the saved work
for later. Versions are automatic; required evidence must pass before submission. Other technicians'
tickets remain inaccessible.

Approver option **1 Pending approvals** lists all pending tickets assigned to that approver with
 their ticket IDs. **2 Review pending ticket** asks for a ticket ID and displays the technician's
response and evidence. Choose **1 Accept** or **2 Reject**. Rejection requires a reason and returns
 the ticket to its existing technician as rejected for rework and resubmission. Review versions load
 automatically. Options **3 Ticket details/history**, **4 Close approved ticket**, and **5 Change password** remain available.

## Simplified staff and admin ticket menus

Both ticket menus now offer **1 Create ticket** and **2 View ticket history** only.
Enter equipment ID and ticket type (corrective, preventive or calibration). Priority is medium
for corrective/preventive and low for calibration. Due date is the UTC registration date plus three
days. Title and employee assignments are automatic; no description or scheduling inputs are required.
Staff history shows only their own requests; admin history shows all visible tickets.
This replaces the earlier admin reassignment menu and staff incident submenu described above.

## Role-based view folders

Views are organized under `ConsoleApp/Views/`:

- `Admin/`: employee, dashboard, audit, exception, reference and equipment-assignment views.
- `Staff/StaffTicketView.cs`: staff ticket creation inputs and confirmation.
- `Technician/TechnicianTicketView.cs`: technician response, checklist and calibration inputs.
- `Approver/ApproverTicketView.cs`: accept/reject inputs and required rejection reason.
- `Shared/`: reusable ticket, equipment and backup displays, console input/menu helpers and logging.

Controllers still coordinate workflows; services still enforce permissions. Shared views do not grant access.
View namespaces match these folders: `CriticalCare.ConsoleApp.Views.<Folder>`.
