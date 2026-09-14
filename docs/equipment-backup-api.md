# Equipment and backup reservations

The backend targets .NET 8. Equipment business logic lives in `EquipmentService`; backup request,
allocation and escalation logic lives in `BackupReservationService`. Controllers only handle HTTP.

## Database setup

New installations run the committed migrations on startup. Existing databases created by the
original `EnsureCreated()` app need the one-time baseline step in the root README first.
`EnsureCreated` does not upgrade existing tables: [EF Core documentation](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created).

Seeding adds a location with code `CENTRAL-WAREHOUSE` and `isCentralWarehouse: true`.
Get its ID from `GET /api/reference/locations`; create or move available equipment to that location
to stock the warehouse. No existing ward equipment is automatically moved. Locations and equipment
types must also be active. The equipment location represents its home/return location; the active
request contains the ward and bed where the item is being used.

## Equipment APIs

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/equipment` | Filtered, paginated equipment |
| GET | `/api/equipment/{id}` | Equipment and computed availability |
| POST | `/api/equipment` | Create equipment |
| PUT | `/api/equipment/{id}` | Edit equipment |
| DELETE | `/api/equipment/{id}` | Soft delete |
| POST | `/api/equipment/{id}/technicians` | Assign a maintenance technician |
| POST | `/api/equipment/{id}/approvers` | Assign a maintenance approver |

Example query:

```http
GET /api/equipment?search=vent&equipmentTypeId=1&isCentralWarehouse=true&availability=available&page=1&pageSize=20
```

Filters: `search` (name/code/serial, case insensitive), `equipmentTypeId`, `locationId`,
`isCentralWarehouse`, `isActive` (defaults to true), and `availability`
(`available`, `reserved`, `in_use`, `inactive`). Use `isActive=false` to list deleted equipment.
Pages start at 1, page sizes range from 1 to 100, and ordering is by equipment ID.
Lists return `{ items, totalCount, page, pageSize, totalPages }`.
Each equipment item and single-equipment response is `{ equipment: { ... }, availability }`.
This replaces the previous unpaginated equipment array response.

Create body (replace the location ID with the warehouse ID):

```json
{
  "equipmentCode": "VENT-001",
  "name": "Backup Ventilator",
  "equipmentTypeId": 1,
  "locationId": 3,
  "serialNumber": "SN-1001",
  "manufacturer": "Acme",
  "modelNumber": "V100",
  "purchaseDate": "2026-09-01",
  "createdBy": 1
}
```

PUT accepts `name`, `equipmentTypeId`, `locationId`, `serialNumber`, `manufacturer`,
`modelNumber`, `purchaseDate`, `isActive`, and `updatedBy`. Equipment code is immutable.
Reserved/in-use equipment cannot be deleted, deactivated, moved or change type (409).
Technician/approver assignments use `{ "userId": 2 }`; these maintenance relationships are
separate from exclusive physical equipment reservations.

## Request and reservation flow

One request represents one item for one ward/bed (`quantity` defaults to 1; other values are rejected).

```http
POST /api/backup-requests
Content-Type: application/json

{
  "requestedWard": "ICU",
  "requestedBy": 2,
  "equipmentTypeId": 1,
  "bedNumber": "ICU-B12",
  "priority": "high",
  "reason": "Temporary replacement"
}
```

Ward values: `ICU`, `ER`, `OT`, `GENERAL`, `OTHER`. Priority values: `low`, `medium`, `high`,
`critical`. Optional `requestedEquipmentId` prefers that particular warehouse item if available.
If it is reserved or in use, the service selects another available item of the same type in the
central warehouse. When omitted, it selects the first available matching item by ID.
Escalation occurs only when no matching active item is available in the central warehouse.

Creation returns HTTP 201 and a detail object:

```json
{
  "request": { "id": 42, "requestedWard": "ICU", "bedNumber": "ICU-B12", "status": "reserved" },
  "reservation": {
    "allocation": {
      "id": 7,
      "requestId": 42,
      "equipmentId": 10,
      "status": "reserved",
      "allocatedAt": "2026-09-14T12:00:00Z",
      "reservationExpiresAt": "2026-09-14T12:20:00Z"
    },
    "equipment": { "id": 10, "equipmentCode": "VENT-001", "name": "Backup Ventilator" },
    "remainingSeconds": 1200
  },
  "allocationHistory": [],
  "escalations": [],
  "serverTimeUtc": "2026-09-14T12:00:00Z"
}
```

This example abbreviates entity fields and history; the actual history also contains the current
allocation. Equipment includes its equipment type and warehouse location. If no item is available,
`request.status` is `escalated`, `reservation` is null, and `escalations` contains the shortage ticket.
The ticket uses the existing `backup_escalations` model because a maintenance `Ticket` requires
a particular equipment ID, which a stock shortage cannot supply. It is assigned to an active admin
with the fewest open escalations, with an in-app notification. If no admin exists, the escalation
remains in the unassigned queue. Repeated reserve attempts do not duplicate an unresolved ticket.

For the reservation page, fetch `GET /api/backup-requests/42/reservation` (also available at
`GET /api/backup-requests/42`). Render the equipment details and countdown using
`reservationExpiresAt` and `serverTimeUtc`, or `remainingSeconds`. Refetch when the countdown
reaches zero and after every action. Refreshing or retrying reserve while reserved does not extend
the deadline. All timestamps are UTC, and expiry is inclusive (`now >= reservationExpiresAt`).

| Method | Route | Body / purpose |
| --- | --- | --- |
| GET | `/api/backup-requests` | Filters and pagination |
| GET | `/api/backup-requests/{id}` | Request, current allocation, equipment, history, escalation |
| GET | `/api/backup-requests/{id}/reservation` | Same detail for reservation page |
| POST | `/api/backup-requests` | Create request and reserve, or escalate |
| POST | `/api/backup-requests/{id}/reserve` | `{ "performedBy": 2 }`, retry after shortage/expiry |
| POST | `/api/backup-requests/{id}/allocations/{allocationId}/pickup` | `{ "performedBy": 2 }` |
| POST | `/api/backup-requests/{id}/allocations/{allocationId}/return` | `{ "performedBy": 2 }`, confirms physical return to warehouse |
| POST | `/api/backup-requests/{id}/cancel` | `{ "performedBy": 2, "reason": "No longer needed" }` |
| GET | `/api/backup-requests/escalations` | `unresolvedOnly=true` (default), `page`, `pageSize` |

Request filters: `requestedBy`, `requestedWard`, `equipmentTypeId`, `status`, `bedNumber`, `page`,
`pageSize`. Results have the same pagination envelope as equipment. All enum responses now use
readable strings; JSON enum inputs also accept the legacy numeric values.

Successful pickup sets request and allocation to `in_use`, and records who picked it up and when.
It then remains occupied until the return API is called, regardless of the old reservation deadline.
Return records who returned it and when, and makes it available for another request. Repeating pickup
or return for the same allocation is idempotent. A stale allocation ID cannot act on a newer reservation.
Cancelled and returned requests cannot be reserved again; create a new request instead.

Expired allocations become `expired`, requests become `unreserved`, and equipment becomes available.
Expiry does not automatically reserve again; the user explicitly retries `/reserve`. A server worker
persists expirations every 15 seconds and on restart. Reservation/detail/list operations also check
expiry, and pickup checks the exact deadline, so the worker interval cannot extend pickup eligibility.
No browser timer is required for server-side release.

## Concurrency and errors

Equipment moves/deletes, reservation, pickup, return, cancellation and expiry share a PostgreSQL
transaction advisory lock. The transaction includes availability checking and allocation changes.
This serializes inventory mutations across API processes. A partial unique database index also
enforces one `reserved`/`in_use` allocation per equipment item and per request.
For higher throughput the lock can later be partitioned, preserving a consistent lock order.
[PostgreSQL transaction locks](https://www.postgresql.org/docs/16/explicit-locking.html#ADVISORY-LOCKS)
are released on commit/rollback, including when an application process disconnects.

Invalid input returns 400, unknown records 404, wrong request actor 403, and unavailable state
transitions/duplicates 409, using Problem Details. Shortages are successful request creations with
`escalated` status, rather than HTTP errors.

The existing app has no login/authentication. Actor checks currently validate the supplied user ID
against the requester/admin records; they do not authenticate the caller. When login is added,
derive the actor ID from authenticated claims rather than trusting `requestedBy`/`performedBy`.

## Integration verification

The tests use real PostgreSQL, separate DbContexts for concurrent staff requests, and a controllable
clock so expiry tests do not wait 20 minutes. They create and remove their own random schema.

```powershell
docker run --detach --rm --name equipment-reservation-tests -e POSTGRES_PASSWORD=reservation_test_only -e POSTGRES_DB=reservation_tests -p 127.0.0.1:55438:5432 postgres:16
dotnet run --project Tests/EquipmentManagementBackend.IntegrationTests.csproj
docker stop equipment-reservation-tests
```

Wait until PostgreSQL is ready before running. Set `TEST_DATABASE_CONNECTION` to override the test
connection. Use a dedicated test database. The suite covers migrations, filtering, pagination,
exclusive reservation, expiry boundaries, stale pickup, escalation, cancellation, return, and
blocking edits/deletion of occupied stock.
