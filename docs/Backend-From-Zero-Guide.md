# Equipment Management Backend: From Zero to Independent Development

Beginner's handbook for this project • Prepared 15 September 2026

This document explains the actual application in this folder. It is intended to be read offline, without an AI assistant. Examples marked “exercise” are instructions for future work, not changes already made to your project. Passwords shown here are placeholders, never your actual password.

## 1. What you are building

Imagine a hospital with a central equipment store. Staff need to know what equipment exists, request a backup item for a ward and bed, reserve it, pick it up, and return it. Maintenance staff also need tickets and schedules.

This project is the backend: the program that receives requests, checks business rules, and stores information. It does not include a finished frontend with hospital screens. Swagger is an interactive testing page, not the final hospital interface.

You do not need a particular IDE to run this project. An IDE is an editor with conveniences such as debugging and autocomplete. The .NET SDK builds and runs the code. PostgreSQL stores the data. These are separate programs.

Think of the application as this path:

```text
Browser / mobile app / Swagger / Postman
                  |
         HTTP request with JSON
                  v
Controller: chooses the operation
                  v
Service: checks rules and performs the operation
                  v
AppDbContext + EF Core + Npgsql: talk to the database
                  v
PostgreSQL: stores rows and enforces database constraints
                  |
         Result travels back as JSON
```

Equipment and backup requests follow this service structure. The older reference, maintenance-schedule, and ticket controllers still access AppDbContext directly for some work. AssignmentService helps ticket creation choose a technician and approver. Do not assume every controller is already separated into a service.

## 2. Learn these names first

| Name | Meaning in plain language | Where you see it |
| --- | --- | --- |
| C# | The programming language used to write the app | Files ending in `.cs` |
| .NET | The platform that executes C# applications | `net8.0` in the project file |
| SDK | Tools for building and developing applications | `dotnet build`, `dotnet run` |
| Runtime | Software needed to execute a built application | Included with the SDK |
| ASP.NET Core | The part of .NET used to build web APIs | Controllers and Program.cs |
| NuGet | The package system for .NET libraries | PackageReference entries |
| Entity | A C# object representing stored business data | Equipment, User, BackupRequest |
| Table / row / column | A collection / one record / one field | PostgreSQL tables |
| Primary key | The identity of a record | Equipment.Id |
| Foreign key | A value pointing to another record | Equipment.EquipmentTypeId |
| DTO | The data accepted or returned by an API | CreateEquipmentRequest |
| Service | A class implementing business rules | EquipmentService |
| Controller | An entry point for HTTP requests | EquipmentController |
| API endpoint | A method and URL clients can call | POST /api/equipment |
| JSON | A text format for sending structured data | `{ "name": "Ventilator" }` |
| EF Core | Library translating supported C# database operations to SQL | AppDbContext queries |
| Npgsql | PostgreSQL driver/provider used by EF Core | UseNpgsql |
| SQL | The database query language | SELECT, INSERT, UPDATE |
| Migration | A versioned instruction set for changing database structure | Migrations folder |
| Transaction | A group of changes committed together or rolled back | Reservation workflow |
| Dependency injection | Framework supplies objects a class needs | Constructor parameters |
| Async / await | Wait for I/O without keeping a thread blocked throughout the wait | SaveChangesAsync |
| pgAdmin | A graphical tool to inspect and manage PostgreSQL | Tables and Query Tool |
| psql | PostgreSQL command-line client | Population script |

An entity is not itself a database table. EF Core reads your entity definitions and mapping rules; migrations create the corresponding database structure. Changing a class alone does not alter an existing table.

## 3. What to install and what to copy

For the existing project, install a .NET 8 SDK and PostgreSQL on the new computer. Install pgAdmin if you want a graphical database browser. A C#-capable IDE is helpful but the terminal commands work independently of the editor.

This project currently targets .NET 8. The installed SDK previously verified on the original computer was 8.0.425. You can use a compatible installed .NET 8 SDK: global.json selects the latest installed .NET 8 feature band starting at 8.0.100. It does not automatically install an SDK or select .NET 10 instead.

Copy the whole project folder, especially these files and directories:

```text
EquipmentManagementBackend.csproj
global.json
.config/dotnet-tools.json
Program.cs
appsettings.json
Properties/
Models/
DTOs/
Data/
Services/
Controllers/
Migrations/                 including Designer and snapshot files
Tests/
scripts/
docs/
docker-compose.yml          optional Docker database setup
```

The folders bin and obj are generated build output and can be regenerated. Your actual PostgreSQL rows are not in these project folders. Copying the source code does not copy the database. Section 20 explains database transfer.

Open a terminal in the folder containing EquipmentManagementBackend.csproj. All project commands in this guide assume that working directory unless stated otherwise.

```powershell
dotnet --list-sdks
dotnet --version
dotnet restore
dotnet tool restore
dotnet build
```

Read the results: the SDK version should begin with 8; restore downloads packages; tool restore installs the project-local EF tool; build should end with “Build succeeded.” Internet access is normally needed for the first package/tool restore. A computer with already cached dependencies can often build offline, but a fresh computer cannot download missing dependencies without network access.

The current project package versions are project choices, not a claim that they will always be the newest releases:

| Package/tool | Version in this project | Purpose |
| --- | --- | --- |
| Microsoft.EntityFrameworkCore | 8.0.31 | Database object/query support |
| Microsoft.EntityFrameworkCore.Relational | 8.0.31 | Relational database functionality; explicitly aligned to avoid assembly mismatch |
| Microsoft.EntityFrameworkCore.Design | 8.0.31 | Migration design-time tooling |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.11 | PostgreSQL integration |
| Swashbuckle.AspNetCore | 8.0.0 | Swagger/OpenAPI testing and description |
| dotnet-ef local tool | 8.0.31 | Migration commands |

Third-party package versions do not all have to match the .NET runtime number exactly. Compatibility matters. Keep related Microsoft EF Core packages aligned when updating them.

## 4. Connect PostgreSQL, step by step

PostgreSQL runs as a separate database server. The application connects using a host, port, database name, username, and password. On the original computer PostgreSQL 18 was installed as a Windows service and listened on port 5432.

In pgAdmin, connect/register a server with host localhost, port 5432, and your PostgreSQL login, commonly postgres. The password is the one you set for that PostgreSQL account during installation. An application User row named “Admin User” is not a PostgreSQL login. These are different systems.

Place the connection in appsettings.json:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=equipment_management;Username=postgres;Password=YOUR_POSTGRES_PASSWORD"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

| Connection component | Explanation |
| --- | --- |
| Host=localhost | PostgreSQL runs on the same computer as this backend process |
| Port=5432 | PostgreSQL's configured listening port; it is different from API port 5080 |
| Database=equipment_management | The database containing this application's tables |
| Username=postgres | A PostgreSQL account allowed to connect and change this database |
| Password=... | That account's password |

Program.cs reads the named connection and registers the database context:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

Read this as: “When something needs AppDbContext, create it configured to use PostgreSQL and this connection string.” AppDbContext is not a database server and UseNpgsql does not install PostgreSQL.

For a new database, run:

```powershell
dotnet ef database update
dotnet run
```

The migration command can create the database if the PostgreSQL account has permission. If it cannot, create an empty equipment_management database in pgAdmin first, owned by the appropriate account, then repeat the command. Program.cs also applies pending migrations at startup and seeds reference rows.

On this project's original database, both migrations have already been applied. Do not run the legacy baseline script just because you switch IDEs.

For a password override without putting it in a shareable file, the app and migration factory support environment configuration:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=equipment_management;Username=postgres;Password=YOUR_POSTGRES_PASSWORD'
dotnet run
```

This override belongs to the current terminal and its child processes. Another terminal may still use the file. Avoid sharing credentials in source control or screenshots. The provided population script and local demo helper read appsettings.json directly, so an environment override for the API does not redirect those helpers.

The API normally starts at http://localhost:5080, according to Properties/launchSettings.json. Open http://localhost:5080/swagger. If the terminal shows another address, use that address. Stop the server with Ctrl+C before rebuilding if Windows reports a locked executable.

## 5. Read a small amount of C# before reading the models

```csharp
public class Equipment
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public bool IsActive { get; set; } = true;
}
```

“class” defines a type of object. “public” allows other code to use it. A property is a named value on that object. “get; set;” allows reading and changing the property. “long” is a whole-number type; “string” is text; “bool” is true or false. A question mark makes a value nullable: SerialNumber may be absent. The initializers give new C# objects starting values; they do not automatically create SQL defaults for every external database insert.

```csharp
var item = new Equipment { Name = "Ventilator" };
db.Equipment.Add(item);
await db.SaveChangesAsync();
```

“var” asks the compiler to infer the type. “new” creates an object. Add tells the context to track a new entity; SaveChangesAsync sends the insert to PostgreSQL. After a successful generated-ID insert, item.Id contains the database-assigned ID.

You will also see modern C# primary constructors:

```csharp
public class EquipmentController(EquipmentService service) : ControllerBase
```

This declares a controller whose constructor requires an EquipmentService. “: ControllerBase” means it inherits useful web-controller behavior. The framework supplies the service through dependency injection. This syntax is supported by the .NET 8 SDK's C# compiler.

## 6. Design the entities from the business requirements

Start with the things you need to remember, not with URLs. We needed users, places, equipment categories, individual equipment, maintenance work, backup requests, reservations, and shortages.

The entities are in Models/CoreModels.cs. There are 16 mapped entity types:

| Entity / table | What one row represents | Important fields |
| --- | --- | --- |
| User / users | A person represented in the application | Id, Name, Email, Role, IsActive |
| Location / locations | An equipment home location | Code, Name, IsCentralWarehouse, IsActive |
| EquipmentType / equipment_types | A category such as ventilator | TypeCode, TypeName |
| Equipment / equipment | One physical item, not a category | EquipmentCode, EquipmentTypeId, LocationId, SerialNumber |
| EquipmentTechnician / equipment_technicians | Maintenance relationship between item and technician | EquipmentId + TechnicianId |
| EquipmentApprover / equipment_approvers | Maintenance relationship between item and approver | EquipmentId + ApproverId |
| EquipmentCalibrationRule / equipment_calibration_rule | A calibration instruction for an item | RuleId, RuleText, EquipmentId |
| Ticket / tickets | Maintenance work for a specific item | TicketId, Status, AssignedTo, ApproverId, VersionNumber |
| TicketHistory / ticket_history | One recorded maintenance ticket action | TicketId, Action, PerformedBy, PerformedAt |
| TicketDocument / ticket_documents | Metadata about a document attached to a ticket | StoragePath, file names, UploadedBy |
| MaintenanceSchedule / maintenance_schedules | A planned maintenance task | EquipmentId, DueDate, ScheduleType, IsCompleted |
| BackupRequest / backup_requests | A person's request for one item at a ward/bed | RequestedBy, RequestedWard, BedNumber, EquipmentTypeId, Status |
| BackupAllocation / backup_allocations | A particular assignment attempt for a backup request | RequestId, EquipmentId, Status, ReservationExpiresAt |
| BackupEscalation / backup_escalations | A stock shortage needing attention | RequestId, AssignedAdminId, ResolvedAt |
| Notification / notifications | A stored message for an application user | RecipientUserId, Title, Message, IsRead |
| AuditLog / audit_logs | A prepared audit-record structure | EntityType, EntityId, Action, RetentionUntil |

Important distinction: EquipmentType is “ventilator”; Equipment is “the ventilator with serial SN-001.” A request states what kind of item is needed; an allocation records which physical item was actually reserved. Separating them preserves history when a reservation expires and a later attempt uses a different item.

Every new backup request must have quantity 1. Multiple items require separate requests. BedNumber is text, so values such as ICU-B12 are possible. RequestedWard is an enum: one of ICU, ER, OT, GENERAL, OTHER.

The current equipment record does not contain an Availability column. Availability is computed from IsActive and allocation records. This avoids maintaining two separate status values that could disagree.

Equipment.LocationId is the home/return location. Pickup does not move the equipment row to the ward. The active BackupRequest records the ward and bed where it is being used.

Some entities are preparation for future features. TicketDocument has no upload endpoint. AuditLog does not mean every request is automatically audited or that archive retention is enforced. Notification rows are created for shortages, but there is no notification delivery, email, or notification-list API. Delivery/receipt fields and some older status values exist but have no corresponding workflow endpoints yet.

## 7. Relationships: how records connect

```text
EquipmentType 1 ---- many Equipment
Location      1 ---- many Equipment
User          1 ---- many BackupRequest
EquipmentType 1 ---- many BackupRequest
BackupRequest 1 ---- many BackupAllocation (history)
Equipment     1 ---- many BackupAllocation (history)
BackupRequest 1 ---- many BackupEscalation (history)
Equipment     1 ---- many Ticket
Ticket        1 ---- many TicketHistory
```

“1 ---- many” means one type can be used by many equipment items, but each equipment item has one equipment type.

Suppose EquipmentType.Id is 1 for ventilators. An Equipment row with EquipmentTypeId=1 refers to that category. A foreign key prevents inserting a reference to a category that does not exist. A navigation property such as Equipment.EquipmentType lets C# code work with the related object when loaded.

EquipmentTechnician and EquipmentApprover have composite keys: two columns together identify the row. This prevents repeating the same maintenance relationship. These assignments are not reservations. Several technicians may maintain the same item; only one active backup allocation can occupy it.

## 8. AppDbContext: the bridge between objects and tables

Open Data/AppDbContext.cs. It inherits from EF Core's DbContext. Its constructor receives database configuration. Each DbSet gives code a way to query or change a particular kind of entity:

```csharp
public DbSet<Equipment> Equipment => Set<Equipment>();
```

This does not load every equipment row at startup. It is a query entry point. OnModelCreating describes mappings and database rules:

```csharp
b.Entity<Equipment>().ToTable("equipment");
b.Entity<Equipment>().HasIndex(x => x.EquipmentCode).IsUnique();
b.Entity<Equipment>().HasOne(x => x.EquipmentType)
    .WithMany().HasForeignKey(x => x.EquipmentTypeId)
    .OnDelete(DeleteBehavior.Restrict);
```

The first line chooses the table name. The second prevents duplicate equipment codes at database level. The remaining lines map a relationship and prevent deleting a referenced equipment type while equipment still points to it.

Restrict rejects physical deletion of referenced data. Cascade physically deletes dependent rows when the parent is deleted. SetNull clears an optional reference. These database behaviors are different from the application's equipment soft delete, which only sets IsActive=false.

The context converts enums to strings in PostgreSQL. A status is stored as “reserved” rather than an integer. Program.cs separately configures enum strings for JSON. Database conversion and HTTP serialization are two separate settings. Renaming an enum member can therefore require migrating stored string values, not just editing C#.

For backup allocations, partial unique indexes apply only to rows with Status reserved or in_use. Historical returned/expired/cancelled rows can coexist. There is one such index per EquipmentId and another per RequestId. BackupEscalation has a unique index per RequestId only while ResolvedAt is null.

Other indexes help frequent searches. Ticket.VersionNumber is a concurrency token, meaning EF checks it during writes to detect another writer's update.

The design-time factory, Data/AppDbContextFactory.cs, creates the context for dotnet ef commands. It lets migration tools inspect the model without starting the web server and its background worker. It reads appsettings.json and environment variables; unlike Program.cs's standard builder, it does not explicitly add appsettings.Development.json.

## 9. Migrations: turn the design into actual tables

We use an EF Core code-first workflow: define the C# model and mapping, generate a migration, review it, and apply it to PostgreSQL.

```text
Edit entities / OnModelCreating
           |
dotnet ef migrations add ChangeName
           |
Generated C# migration + Designer + updated model snapshot
           |
Review the generated operations
           |
dotnet ef database update
           |
PostgreSQL executes schema changes and records migration history
```

The EF tool compares the current model with its stored model snapshot when generating a migration. It is not comparing your current table data row by row. This is why manually adding columns in pgAdmin can make the model history and database disagree. See the [EF Core migration overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/) for the official description.

Your files have three different jobs:

| File | Job | Should a beginner edit it? |
| --- | --- | --- |
| Timestamp_Name.cs | Up applies changes; Down describes reversal | Review every time; edit intentionally when data transformation is necessary |
| Timestamp_Name.Designer.cs | Generated model metadata associated with that migration | Normally no |
| AppDbContextModelSnapshot.cs | Generated description of the latest model for the next comparison | Normally no |

The Designer.cs tab you have open is generated output. To add an equipment field, start in Models/CoreModels.cs, not in the Designer file.

The two committed migrations are:

1. 20260914160344_InitialSchema: creates the original 16-table schema and relationships.
2. 20260914161134_BackupReservationWorkflow: adds bed numbers, central warehouse flag, allocation lifecycle/timestamps, nullable escalation assignment, and unique active-allocation indexes.

The second migration also transforms existing allocation data. Existing non-terminal allocations are conservatively treated as in_use so an upgrade cannot accidentally offer already occupied equipment. It preserves existing records instead of clearing the database. Duplicate historical active assignments or open escalations must be reconciled if they prevent new unique indexes from being created.

Useful commands:

```powershell
dotnet tool restore
dotnet ef migrations list
dotnet ef database update
dotnet ef migrations has-pending-model-changes
dotnet ef migrations script --idempotent --output migration-review.sql
```

The migration history table is named __EFMigrationsHistory. A migration is recorded there once applied. Starting the app again does not reinsert all equipment or reapply an already recorded migration.

The original scaffold used EnsureCreated, which creates a new schema but does not evolve an existing schema through migrations. We replaced it with MigrateAsync. The legacy Data/baseline-existing.sql script is only for an original pre-migration EnsureCreated database whose existing schema matches the original model. It records the initial migration before the workflow upgrade. It is not needed for a new database, this already migrated database, or a normal restored copy of it.

Do not delete migration files to fix a database error, and do not routinely recreate migrations already applied elsewhere. For a wrong, unapplied latest migration, dotnet ef migrations remove can remove it after you understand its state. For applied/shared changes, a new corrective migration is usually clearer. Down operations can lose data, so inspect them before any rollback. Official deployment guidance is in [Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

## 10. DTOs: choose what the client can send

An entity describes storage. A request DTO describes input for one action. They should not automatically be the same thing. A client creating equipment should not choose its database Id or assign itself a reservation state.

DTOs/Requests.cs contains CreateEquipmentRequest, UpdateEquipmentRequest, CreateBackupRequestRequest and ticket/assignment inputs. DTOs/WorkflowDtos.cs contains filters, pagination, and response wrappers.

```csharp
public record BackupActorRequest([Range(1, long.MaxValue)] long PerformedBy);
```

A record is a concise data-carrying C# type. This record has one value, PerformedBy. The Range annotation requires a positive ID during HTTP validation. For positional records, validation attributes belong on constructor parameters, not property-targeted attributes. A real HTTP test caught and corrected this issue during development.

Annotation validation is not enough for business rules. An ID of 99 may be positive but refer to no actual user. The service checks the database for an active user. Equipment creation also checks lengths, unique codes, and active type/location references.

PagedResult returns Items, TotalCount, Page, PageSize, and TotalPages. An equipment response wraps the stored entity plus computed Availability. A backup detail response includes the request, current reservation, allocation history, escalation history, and server UTC time.

## 11. EquipmentService: CRUD, filtering, and pagination

CRUD means create, read, update, delete. Open Services/EquipmentService.cs.

CreateAsync validates input, checks the referenced type/location/user, checks the code, creates an Equipment object, saves it, and returns details. A database unique index protects against duplicate codes even if two requests pass the initial application check at almost the same time.

GetAsync finds the item, includes its type/location, and computes availability. ListAsync adds optional filters to a database query. UpdateAsync changes editable fields. DeleteAsync performs a soft delete. AssignAsync manages maintenance technician/approver links.

```csharp
var items = db.Equipment.AsNoTracking();
items = items.Where(e => e.IsActive);
var page = await items.OrderBy(e => e.Id)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

This teaching excerpt is the shape of the query, with simplified variable names. Where filters records. OrderBy creates a stable order. Skip passes over previous pages. Take limits the result. ToListAsync executes the query. For page 3 and page size 10, Skip is 20, so you receive the next ten records.

AsNoTracking avoids tracking objects that you only intend to read. Include loads related data such as Location. AnyAsync checks existence. CountAsync counts matching records. SingleOrDefaultAsync expects at most one match. FirstOrDefaultAsync picks the first match or returns null. EF translates supported expressions to SQL, so filtering and pagination happen in the database rather than loading everything into memory first.

The implementation uses an intermediate anonymous projection for filtering computed availability, then constructs EquipmentView results. This was checked against PostgreSQL because not every C# expression can be translated into SQL; an earlier record-constructor projection could not be filtered by EF.

Default IsActive filtering is true; page starts at 1; default page size is 20 and maximum is 100. Search checks name, code, and serial number case-insensitively. An active item is available when there is no in-use allocation and no unexpired reservation.

Before deleting, deactivating, moving, or changing the type of occupied equipment, the service rejects the operation with 409. Editing descriptive fields such as Name remains possible. The equipment code is not editable through the current update DTO.

## 12. BackupReservationService: follow a real request

Suppose there are two available ventilators, A and B, in the central warehouse. Staff 4 requests a ventilator for ICU bed B12 and prefers A.

1. The controller deserializes JSON into CreateBackupRequestRequest.
2. CreateAsync checks bed number, quantity=1, ward, priority, and active references.
3. It begins the inventory transaction and takes the shared database lock.
4. It releases expired reservations within that transaction.
5. It inserts a BackupRequest and obtains its generated Id.
6. ReserveWithinTransactionAsync searches active items of that type at active central warehouse locations with no active allocation.
7. It prefers requestedEquipmentId if available, otherwise the lowest-ID matching available item.
8. It creates a reserved BackupAllocation with a deadline 20 minutes after allocation.
9. It changes the request to reserved and resolves any open shortage escalation for that request.
10. It saves and commits, then returns the request, actual selected equipment, allocation ID, and timing fields.

Supplying a preferred item still requires that it exists, is active, matches the type, and belongs to the active central warehouse. An invalid ID is a 400 input error. An occupied but valid preferred item triggers fallback selection, not an immediate shortage.

When staff 5 also asks for A, the service sees that A is occupied and reserves B. When a third request arrives and neither A nor B is available, the request becomes escalated. This is a successful creation with a shortage outcome, not a failed HTTP request.

Escalation uses BackupEscalation, not the maintenance Ticket entity. A maintenance Ticket requires a particular EquipmentId; a shortage may have no available physical item. The service assigns an open escalation to the active admin with the fewest unresolved escalations, then lowest ID. If there is no active admin, AssignedAdminId stays null. A Notification row is inserted when an admin is selected.

The same request cannot have duplicate unresolved escalations. Retrying after equipment becomes available resolves its existing escalation. There is no automatic first-in-first-out waiting queue: an escalated request needs an explicit reserve retry. Priority is recorded but does not currently reorder an automatic allocation queue.

## 13. Why two people cannot reserve the same equipment

A normal “check then insert” is unsafe by itself:

```text
Staff A reads: item free
Staff B reads: item free
Staff A inserts a reservation
Staff B inserts another reservation
```

The application uses two protections. First, InventoryTransaction begins a PostgreSQL transaction and calls pg_advisory_xact_lock with a shared numeric key. All application instances using this database and workflow use the same key. One inventory operation waits while another holds the lock; then it rechecks availability using committed data. This is a transaction-level application lock, not a browser lock or a separate per-equipment row lock.

Second, PostgreSQL's partial unique indexes reject two active allocations for the same item or request. This protects the invariant even if someone writes outside the normal service. Such a conflicting external write is mapped to a conflict response; normal concurrent service requests are serialized and can select alternatives.

```text
Staff A: acquire lock -> choose A -> commit/release lock
Staff B: wait         -> acquire -> see A occupied -> choose B -> commit
Staff C: acquire      -> see no matching free stock -> escalate -> commit
```

Commit makes the transaction permanent. If an error occurs before commit, disposal rolls back that transaction's changes. The advisory lock is automatically released when the transaction ends. This coarse lock favors straightforward correctness over maximum throughput; it serializes inventory work, including some reads that process expiry. PostgreSQL describes transaction advisory locks in its [locking documentation](https://www.postgresql.org/docs/18/explicit-locking.html#ADVISORY-LOCKS).

Do not replace this with a C# lock statement alone. A C# lock coordinates one application process, not multiple backend instances connected to the same database. Also, DbContext is not thread-safe: concurrent test requests use separate contexts.

## 14. Reservation states and the 20-minute timer

| Event | Request status | Allocation status | Equipment availability |
| --- | --- | --- | --- |
| Matching item selected | reserved | reserved | reserved |
| No matching stock | escalated | No current allocation | No change to other equipment |
| Pickup before deadline | in_use | in_use | in_use |
| Deadline reached without pickup | unreserved | expired | available if active |
| Return after pickup | returned | returned | available if active |
| Cancel before pickup | cancelled | cancelled if one was reserved | available if active |

Expiry is inclusive: now greater than or equal to ReservationExpiresAt is too late. Pickup changes status and records PickedUpBy/PickedUpAt. A picked-up item does not expire after twenty minutes; it stays in_use until returned. Return means the caller confirms physical return to the warehouse; the software cannot sense physical movement.

The timer is stored as timestamps, not only as a countdown in the browser. ReservationExpiryWorker runs while the backend runs, immediately and then roughly every 15 seconds. It creates a fresh dependency-injection scope, gets a scoped service, and calls ExpireAsync. The worker logs failures and retries in a later cycle.

Reservation operations and request detail/list reads also process expiry. Pickup additionally checks the exact deadline. Even if the worker has not yet persisted an expiry, it does not grant a grace period for pickup. If the app is stopped, no worker is running; on restart it catches up using persisted deadlines.

The frontend can calculate remaining seconds from ReservationExpiresAt and ServerTimeUtc or use RemainingSeconds. It should refresh after actions and when the timer reaches zero. The server decides validity; a browser's inaccurate clock cannot extend the reservation.

Pickup and return URLs include both requestId and allocationId. A request can have many historical allocations. Using the particular allocation ID prevents an old page from picking up a newer reservation after its previous one expired. Retrying reserve on an already reserved request does not reset the deadline. Repeating pickup on the same in-use allocation or return on an already returned allocation does not create another assignment.

Do not confuse all enum values with implemented actions. Older request statuses such as delivered or received remain defined but there are no deliver/receive endpoints in this version.

## 15. Program.cs and dependency injection: how everything starts

Read Program.cs from top to bottom:

1. WebApplication.CreateBuilder creates configuration, logging, and the service container.
2. AddControllers registers controller support. JSON settings use enum strings and ignore reference cycles during serialization.
3. AddProblemDetails and AddExceptionHandler register structured error handling.
4. Swagger services generate the API description and testing page.
5. AddDbContext registers PostgreSQL-backed AppDbContext.
6. AddScoped registers AssignmentService, EquipmentService, and BackupReservationService.
7. AddSingleton(TimeProvider.System) provides a clock abstraction.
8. AddHostedService registers the expiry worker.
9. Build constructs the application.
10. UseExceptionHandler installs error handling; Swagger is enabled only in Development.
11. UseHttpsRedirection enables HTTPS redirection when configured; MapControllers maps controller routes.
12. A startup scope applies pending migrations and seeds reference data.
13. Run starts serving requests and running hosted services.

Scoped means a service instance belongs to a scope, usually one HTTP request. Its DbContext belongs to that scope too. Singleton means one registered instance for the lifetime of this application process. The hosted worker lives longer than a request, so it creates scopes instead of holding one shared DbContext forever.

Constructor dependency injection makes the chain explicit:

```text
EquipmentController needs EquipmentService
EquipmentService needs AppDbContext, BackupReservationService, TimeProvider
BackupReservationService needs AppDbContext and TimeProvider
```

You normally do not write new EquipmentService inside a controller. The framework constructs it using the registrations. If a class is not registered, startup/request handling can report “Unable to resolve service.” ASP.NET Core's [fundamentals guide](https://learn.microsoft.com/en-in/aspnet/core/fundamentals/?view=aspnetcore-8.0) introduces configuration, middleware, and dependency injection.

## 16. Controllers and HTTP, with one complete example

```csharp
[ApiController]
[Route("api/equipment")]
public class EquipmentController(EquipmentService service) : ControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));
}
```

Route gives the prefix. HttpGet adds an HTTP verb and suffix. The long route constraint expects a whole-number ID. GET /api/equipment/1 runs Get with id=1. CancellationToken allows a cancelled request to stop cancellable database work. Ok produces HTTP 200 with JSON. Task<IActionResult> means the method asynchronously returns an HTTP result.

For POST /api/equipment, ASP.NET deserializes the JSON body into CreateEquipmentRequest, validates supported metadata, and invokes the controller. The controller calls the service. The service validates and inserts using the context. The controller returns CreatedAtAction, which provides status 201 and a URL for the new record.

| HTTP result | Meaning here |
| --- | --- |
| 200 | Successful read/update/action, including a reserve retry that still reports escalation |
| 201 | New equipment, backup request, or maintenance ticket created |
| 204 | Successful operation with no response body, such as equipment soft delete |
| 400 | Invalid input or reference |
| 403 | Supplied backup actor is neither requester nor admin |
| 404 | Requested record not found |
| 409 | Duplicate or invalid state transition, occupied item restriction, or version conflict |
| 500 | Unexpected server error; inspect the backend log |

ServiceExceptionHandler maps known service errors, database uniqueness errors, and foreign-key errors to Problem Details. It does not convert every unexpected exception into a business error. Some older controllers return simpler error bodies.

There is currently no authentication/login. Checking a supplied PerformedBy against the requester/admin record is not proof of identity: a caller can submit another ID. When adding login, derive the actor from authenticated claims and enforce authorization. This is a current application limitation, not a completed security feature.

## 17. Complete manual API reference

Start with dotnet run and open http://localhost:5080/swagger. Expand an operation, click Try it out, enter fields, then Execute. POST/PUT/PATCH JSON uses Content-Type: application/json. Replace IDs with your actual reference and response values; new computers can assign different IDs.

| Method | Path | Purpose/body |
| --- | --- | --- |
| GET | /api/reference/equipment-types | Active types |
| GET | /api/reference/locations | Active locations; inspect IsCentralWarehouse |
| GET | /api/reference/users | Active users |
| GET | /api/equipment | Equipment filters and pagination |
| GET | /api/equipment/{id} | One item plus availability |
| POST | /api/equipment | CreateEquipmentRequest |
| PUT | /api/equipment/{id} | UpdateEquipmentRequest |
| DELETE | /api/equipment/{id} | Soft delete |
| POST | /api/equipment/{id}/technicians | UserId |
| POST | /api/equipment/{id}/approvers | UserId |
| GET | /api/backup-requests | Request filters and pagination |
| GET | /api/backup-requests/{id} | Details, current allocation, history, escalations |
| GET | /api/backup-requests/{id}/reservation | Same detail response for reservation page |
| POST | /api/backup-requests | Create and attempt allocation |
| POST | /api/backup-requests/{id}/reserve | PerformedBy; retry |
| POST | /api/backup-requests/{id}/allocations/{allocationId}/pickup | PerformedBy |
| POST | /api/backup-requests/{id}/allocations/{allocationId}/return | PerformedBy |
| POST | /api/backup-requests/{id}/cancel | PerformedBy and Reason |
| GET | /api/backup-requests/escalations | unresolvedOnly, page, pageSize |
| GET | /api/tickets | List maintenance tickets |
| GET | /api/tickets/{id} | Ticket details |
| POST | /api/tickets | Create maintenance ticket |
| PATCH | /api/tickets/{id}/status | Status, PerformedBy, Remarks, VersionNumber |
| GET | /api/tickets/{id}/history | Recorded actions |
| GET | /api/maintenance-schedules | List schedules |
| POST | /api/maintenance-schedules | Create schedule |

These are 26 method/path combinations. There are no generic create/update/delete reference-data APIs, upload APIs, or standalone notification/audit APIs in the current controllers.

Create equipment:

```http
POST /api/equipment
```

```json
{
  "equipmentCode": "MANUAL-VENT-001",
  "name": "Manual Ventilator",
  "equipmentTypeId": 1,
  "locationId": 3,
  "serialNumber": "MANUAL-SN-001",
  "manufacturer": "Demo",
  "modelNumber": "V100",
  "purchaseDate": "2026-09-15",
  "createdBy": 1
}
```

Update equipment uses the same editable descriptive fields, type and location, but replaces createdBy with updatedBy, includes isActive, and omits equipmentCode:

```json
{
  "name": "Updated Ventilator",
  "equipmentTypeId": 1,
  "locationId": 3,
  "serialNumber": "MANUAL-SN-001",
  "manufacturer": "Demo",
  "modelNumber": "V200",
  "purchaseDate": "2026-09-15",
  "isActive": true,
  "updatedBy": 1
}
```

Technician and approver assignment bodies are `{ "userId": 2 }`, with the appropriate user's ID.

```http
GET /api/equipment?equipmentTypeId=1&isCentralWarehouse=true&availability=available&page=1&pageSize=10
GET /api/equipment?search=SAMPLE-&pageSize=100
GET /api/equipment?isActive=false
```

Equipment filters: search, equipmentTypeId, locationId, isCentralWarehouse, isActive, availability, page, pageSize. Availability values: available, reserved, in_use, inactive.

Create a backup request:

```json
{
  "requestedWard": "ICU",
  "requestedBy": 2,
  "equipmentTypeId": 1,
  "bedNumber": "ICU-B12",
  "requestedEquipmentId": null,
  "quantity": 1,
  "priority": "high",
  "reason": "Temporary replacement"
}
```

Priority values are low, medium, high, critical. RequestedEquipmentId may be omitted; if present and valid it is a preference. After creation, save request.id and reservation.allocation.id, not just equipment.id. When escalated, reservation is null and there is no current allocation ID to pick up.

Reserve/pickup/return body:

```json
{ "performedBy": 2 }
```

Cancel body:

```json
{ "performedBy": 2, "reason": "No longer needed" }
```

Request filters: requestedBy, requestedWard, equipmentTypeId, status, bedNumber, page, pageSize. Example: GET /api/backup-requests?requestedBy=2&status=reserved&page=1&pageSize=10. Escalations default to unresolvedOnly=true; use false to include resolved history.

Create maintenance ticket:

```json
{
  "equipmentId": 1,
  "createdById": 1,
  "title": "Preventive inspection",
  "description": "Check equipment operation",
  "priority": "medium",
  "dueDate": "2026-09-30"
}
```

Ticket creation consults AssignmentService. It counts active maintenance workload for equipment-linked technicians/approvers and picks the least loaded, breaking ties by user ID. If no candidate exists, the assignment stays null. The existing candidate query filters active assignment links; it does not itself also filter User.IsActive. Automatic reassignment and a complete approval policy are not implemented.

Change ticket status using the latest VersionNumber returned by the API:

```json
{
  "status": "in_progress",
  "performedBy": 2,
  "remarks": "Inspection started",
  "versionNumber": 1
}
```

Possible ticket statuses: open, in_progress, pending_approval, approved, rejected, completed, overdue, cancelled. The controller increments VersionNumber and appends TicketHistory. A stale version returns 409. It currently allows status changes without enforcing a full transition or role matrix.

Create maintenance schedule:

```json
{
  "equipmentId": 1,
  "ticketId": null,
  "dueDate": "2026-09-30",
  "scheduleType": "preventive_maintenance",
  "isCompleted": false,
  "createdBy": 1,
  "updatedBy": null
}
```

The schedule controller currently accepts the entity and stores it; it is a simple scaffold. A schedule is not yet a background job that automatically creates tickets when due.

## 18. Populate data and inspect PostgreSQL yourself

There are three separate kinds of initialization:

| Mechanism | What it does |
| --- | --- |
| Migrations | Create/change tables, keys, columns and indexes; the workflow migration also transforms legacy data |
| DbSeeder.SeedAsync | Adds initial users, standard types and locations when their collections are empty; adds central warehouse by code if missing |
| Populate-Equipment.ps1 | Explicitly inserts labeled sample inventory, not run automatically at app startup |

After the API has started once to seed references, run from the project root:

```powershell
.\scripts\Populate-Equipment.ps1
```

This script reads the application connection and invokes psql on Data/sample-equipment.sql. It creates three items for each of seven standard types, 21 total. It finds type/location IDs by code and uses stable SAMPLE- equipment codes. ON CONFLICT DO NOTHING skips those codes on repeat runs, including soft-deleted rows; it does not reset reservations or overwrite existing equipment. The previously inserted two demo ventilators use a separate demo type and are not part of these 21 items.

If PostgreSQL is installed at another path, pass -PsqlPath. If using pgAdmin instead, select the application database, open Query Tool, load Data/sample-equipment.sql and execute it. Do not run PostgreSQL SQL directly as a PowerShell command; SQL belongs in pgAdmin Query Tool or psql.

For ordinary real inventory, use POST /api/equipment so service validation runs. Use unique equipment codes and actual reference IDs. New active items with no allocation are available automatically.

Read-only SQL examples for pgAdmin:

```sql
SELECT "Id", "EquipmentCode", "Name", "EquipmentTypeId", "LocationId", "IsActive"
FROM equipment ORDER BY "Id";

SELECT e."Id", e."EquipmentCode", t."TypeName", l."Name" AS "HomeLocation"
FROM equipment e
JOIN equipment_types t ON t."Id" = e."EquipmentTypeId"
JOIN locations l ON l."Id" = e."LocationId"
ORDER BY e."Id";

SELECT "Id", "RequestId", "EquipmentId", "Status", "ReservationExpiresAt"
FROM backup_allocations ORDER BY "Id" DESC;

SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory";
```

The quotes matter. This project maps table names to lowercase but column names retain PascalCase, such as "EquipmentTypeId". PostgreSQL folds unquoted identifiers to lowercase, so unquoted EquipmentTypeId is not the same identifier as "EquipmentTypeId".

SQL availability is a derived concept here. Do not look for equipment.Status or change an allocation manually to “available.” Use the return/cancel endpoints to preserve the related request state and history.

## 19. Develop a new feature without an assistant

Use this order: business requirement, entity/storage design, DTO, mapping, service, controller, registration, migration if needed, and tests. You do not need a migration for changes that only alter selection logic, validation, or HTTP routing without changing the mapped database model.

Exercise: add an optional AssetTag to equipment. Do this only when you want that feature; the guide does not apply it automatically.

Step 1: define the rule. An equipment item may have an asset tag of at most 80 characters. Existing equipment may have none. Decide whether tags must be unique before adding an index.

Step 2: add to the Equipment class in Models/CoreModels.cs:

```csharp
[MaxLength(80)] public string? AssetTag { get; set; }
```

Step 3: append an optional parameter to both equipment request records, after their current parameters:

```csharp
string? AssetTag = null
```

Do not paste that as a standalone line in the file; add it inside each record's parameter list with the preceding comma. Keeping it optional at the end preserves existing positional callers, including your tests.

Step 4: in EquipmentService.CreateAsync, validate that request.AssetTag is at most 80 characters before creating the entity. Assign AssetTag = request.AssetTag in the object initializer. In UpdateAsync, perform the same length validation and assign equipment.AssetTag = request.AssetTag. A simple guard is:

```csharp
if (request.AssetTag?.Length > 80)
    throw new ServiceException(400, "Asset tag must be at most 80 characters.");
```

Step 5: EquipmentView already returns the Equipment object, so the new public field will appear in that wrapped JSON without changing its constructor. If you later use a flattened DTO, explicitly map the new field there. No extra DbSet is needed because you are extending Equipment rather than introducing a new entity.

Step 6: stop any running executable that blocks compilation. Generate the change:

```powershell
dotnet build
dotnet ef migrations add AddEquipmentAssetTag
```

Step 7: inspect the generated migration. Expect one nullable character-varying column on equipment with length 80. If it proposes dropping unrelated tables/columns, investigate before applying it. Save a database backup before schema changes to important data.

Step 8: apply and verify:

```powershell
dotnet ef database update
dotnet ef migrations has-pending-model-changes
dotnet run
```

Step 9: create/edit an item with assetTag in Swagger, fetch it, and check the column in pgAdmin. Test an 81-character value and an omitted/null value. Existing rows should have null, not disappear. Commit the entity, DTO, service, migration, Designer and snapshot changes together.

For a completely new entity, also add its DbSet and mapping rules, define references and delete behavior, implement service methods, add controller routes and register the service. New endpoints should call services; avoid copying database/business logic into each controller. If a new operation changes inventory availability, preserve the inventory transaction and unique-index invariants.

To recreate a small learning version from an empty folder rather than opening this project, begin with:

```powershell
dotnet new webapi --use-controllers --framework net8.0 -n EquipmentLearning
cd EquipmentLearning
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.31
dotnet add package Microsoft.EntityFrameworkCore.Relational --version 8.0.31
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.31
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.11
dotnet add package Swashbuckle.AspNetCore --version 8.0.0
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 8.0.31
```

Run that learning project against a separate database such as equipment_learning. Add EquipmentType, Location and Equipment first, then the context, connection, service and controller; generate its own initial migration. Add backup requests and allocations only after basic CRUD works. Do not generate a second unrelated initial migration against the existing equipment_management database.

## 20. Move to another IDE or computer, including your data

Same computer, different IDE: open the project folder or .csproj in the other editor, open a terminal in that folder, and use dotnet build/run. PostgreSQL continues running independently. You do not need to recreate the database. Avoid two API processes trying to bind the same port.

Different computer: copy the source and migration files, install the SDK/database tools, restore packages/tools, and configure the new database connection. Choose one data strategy: start fresh using migrations and seeds, or restore a database backup with existing rows.

For a fresh database, follow section 4. IDs may differ from earlier demonstrations. Use the reference endpoints rather than assuming type 8 or user 4 exists.

For existing data, use pg_dump. These Windows examples assume PostgreSQL tools are installed at the shown path; replace the path on the destination if needed. The commands prompt for your PostgreSQL password.

On the source computer:

```powershell
& 'C:\Program Files\PostgreSQL\18\bin\pg_dump.exe' -h localhost -p 5432 -U postgres -W -Fc -f equipment_management.backup equipment_management
```

Copy equipment_management.backup securely. It contains your database records. On the destination, create a NEW empty database to restore into:

```powershell
& 'C:\Program Files\PostgreSQL\18\bin\createdb.exe' -h localhost -p 5432 -U postgres -W equipment_management_copy
& 'C:\Program Files\PostgreSQL\18\bin\pg_restore.exe' -h localhost -p 5432 -U postgres -W --no-owner --no-privileges --exit-on-error -d equipment_management_copy equipment_management.backup
```

Point the copied application at equipment_management_copy, then run dotnet ef migrations list and dotnet run. Do not create tables through migrations before restoring into that empty destination: the backup already includes schema and migration history. Restore into a new database avoids overwriting an existing application database.

Use compatible PostgreSQL versions and tools; matching version 18 to the original local installation is straightforward. pg_dump backs up one database, not every server login/role. The example restores ownership to the destination account with --no-owner. See PostgreSQL's [pg_dump documentation](https://www.postgresql.org/docs/18/app-pgdump.html) for supported formats and options.

The optional docker-compose.yml starts another PostgreSQL server, not a view of your locally installed server. It uses port 5432 by default, so do not start it on that port while local PostgreSQL already occupies it. A database inside Docker has separate storage from your Windows-installed database.

## 21. Test and debug independently

A successful build proves the code compiles; it does not prove the password works, SQL queries translate, HTTP DTOs bind, or two users cannot double-book. Use layers of verification.

First run dotnet build. Then inspect migrations and call simple GET APIs. Next test create/edit/delete and invalid data. Finally test workflow sequences and simultaneous callers against PostgreSQL.

The Tests project is a console-based integration harness with assertions, not an xUnit/MSTest suite. Run it with dotnet run, not just dotnet test. Its normal mode creates a random schema in a dedicated test database and removes that schema in finally. It tests real PostgreSQL transactions and a controllable clock. Its earlier verified suite contained 43 checks, including migration compatibility, fallback allocation, exact expiry, and unique-index enforcement.

With Docker installed and running, an isolated test setup is:

```powershell
docker run --detach --rm --name equipment-reservation-tests -e POSTGRES_PASSWORD=reservation_test_only -e POSTGRES_DB=reservation_tests -p 127.0.0.1:55438:5432 postgres:16
docker exec equipment-reservation-tests pg_isready -U postgres -d reservation_tests
dotnet run --project Tests/EquipmentManagementBackend.IntegrationTests.csproj --configuration Release
docker stop equipment-reservation-tests
```

Wait until pg_isready reports ready before running the suite. If using local PostgreSQL instead, create a dedicated test database and set TEST_DATABASE_CONNECTION in your terminal to its connection string. Do not point ordinary integration tests at important application data. The sample test password above is only for the disposable test container.

The explicit --seed-local-demo mode is different: it reads your application file connection and retains labeled demo records/history. It performed ten local checks in this project. It simulates expiry by moving only its demo allocation's timestamps into the past; it does not literally wait twenty minutes. Use the normal sample equipment population script when you only want inventory, rather than a workflow demonstration.

Tests/HttpSmoke.ps1 exercises an already running isolated test API. It creates test records, so use it deliberately against a test instance. The default URL is http://127.0.0.1:5089. It checks Swagger, HTTP validation, CRUD, and workflow routes. Real HTTP checks previously caught a record-annotation problem that direct service calls could not detect.

To debug in any IDE, set breakpoints in this order: controller action, service method, and the line before SaveChangesAsync. Send one Swagger request. Inspect the DTO, selected item, current status and IDs. Step through the call chain. Use PostgreSQL read queries from section 18 to verify committed rows afterward.

## 22. Troubleshooting table

| Symptom | What to inspect | Practical next step |
| --- | --- | --- |
| dotnet command not found | SDK installation and PATH | Install the SDK, reopen terminal, run dotnet --list-sdks |
| Compatible SDK not found | global.json versus installed SDKs | Install a .NET 8 SDK rather than deleting version constraints blindly |
| dotnet ef not found | Local tool manifest and restore | Run dotnet tool restore in the project folder |
| NuGet restore fails | Network, proxy, package source | Read the first restore error; reconnect/configure access |
| Password authentication failed | PostgreSQL login, saved file, environment override | Confirm password in pgAdmin; save settings; rerun without --no-build |
| Database does not exist | Database name or create permission | Create an empty target database or allow migration creation |
| Connection refused | PostgreSQL service, host, port | Start/check the server and configured port |
| Address already in use | API port or Docker/database port | Stop your duplicate process or choose a different port |
| Column does not exist | Model/schema mismatch | Check pending migrations and correct connection target |
| Relation already exists during initial migration | Old EnsureCreated schema or wrong migration history | Investigate legacy baseline procedure; do not drop tables blindly |
| File locked during build | Running/debugging application | Stop its process or use a separate Release build for tests |
| Could not translate LINQ | Expression is not supported as SQL | Simplify query/projection and verify with actual PostgreSQL |
| Invalid active type/location/user | Supplied foreign ID | Use reference endpoints and select active records |
| Duplicate code / 409 | Unique constraint | Use a new code; do not reset existing records to seed data |
| Pickup returns 409 | Deadline or allocation state | Refresh details, retry reserve if appropriate, use new allocation ID |
| Unexpected shortage | Type, warehouse flag, active flags, allocations | Check every candidate condition, not just total equipment count |
| Swagger missing | Development environment and URL | Use the supplied launch profile; inspect terminal address |
| HTTPS port warning in local HTTP test | HTTPS redirection configuration | The supplied profile is HTTP; configure HTTPS explicitly when needed |
| Windows event-log access error | Process permissions/logging environment | Run with appropriate local permissions or configure an accessible logging provider |
| Enum value cannot be read | Renamed enum strings in existing data | Add a deliberate data migration for the stored values |

When using --no-build, old compiled code or copied configuration may be used. After changing code or configuration, rebuild. In the earlier connection setup, rebuilding was necessary to pick up the saved password change in the migration execution path.

## 23. What was built and what you should learn next

The work completed in this project included installing and selecting .NET 8, aligning EF packages, moving equipment CRUD into a service, adding filter/pagination responses, implementing the backup reservation lifecycle, preventing duplicate active assignments, adding fallback selection for an occupied preferred item, adding expiry and escalation handling, replacing EnsureCreated with migrations, connecting local PostgreSQL, and adding sample data/scripts and real database/HTTP verification.

The database on the original computer was created through the two migrations and seeded with references. Two separate demo items were inserted for workflow verification, then 21 stable SAMPLE- items were added. These are previous results, not a promise that a copied/new database already has those rows. Always query the destination database.

The current scope is a working development backend. Important future work includes authenticated identities, authorization, stronger validation and service separation for the older controllers, production migration/deployment practices, audit/event delivery, and any intended delivery/receipt or scheduled-maintenance automation. There is no frontend included. A frontend running on another origin may also require deliberate CORS configuration, which is not currently added in Program.cs.

Suggested learning order:

1. Run the existing app and read one equipment record in Swagger and pgAdmin.
2. Learn C# classes, properties, nullable values, methods and async/await using these files.
3. Create an item with the API and follow it into PostgreSQL.
4. Trace a backup request through controller, service, context and allocation rows.
5. Perform the AssetTag exercise on a development copy/database.
6. Write a test for the field, including invalid input.
7. Practice backup/restore into a separate database and run the copied app against it.

You are ready to extend a feature when you can answer: What data changes? Which service owns the rule? Does it change availability? Which API accepts it? Does it need a migration? How will I test its normal, invalid, and concurrent cases?

## 24. Offline navigation and official references

This document is self-contained for the project workflow. Keep it with the source code. Read source files in this order: Models/CoreModels.cs, Models/Enums/DomainEnums.cs, Data/AppDbContext.cs, DTOs/Requests.cs, Services/EquipmentService.cs, Controllers/EquipmentController.cs, Program.cs, then BackupReservationService and the migrations.

For deeper study when internet is available:

- ASP.NET Core fundamentals: https://learn.microsoft.com/en-in/aspnet/core/fundamentals/?view=aspnetcore-8.0
- EF Core migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- Applying migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
- PostgreSQL beginner tutorial: https://www.postgresql.org/docs/18/tutorial.html
- PostgreSQL transaction locks: https://www.postgresql.org/docs/18/explicit-locking.html#ADVISORY-LOCKS
- PostgreSQL backups: https://www.postgresql.org/docs/18/app-pgdump.html

Treat your actual source files as the authority for application behavior after future edits. Update this guide along with significant model or workflow changes.
