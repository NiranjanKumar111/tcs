# Populate equipment

Run from the project folder in PowerShell:

```powershell
.\scripts\Populate-Equipment.ps1
```

This reads the connection from `appsettings.json` and inserts 21 labeled sample items into the
central warehouse: three each of ventilators, defibrillators, patient monitors, infusion pumps,
syringe pumps, dialysis machines, and suction machines. Reference IDs are resolved by code.
Run the application once first to apply migrations and seed the standard reference records.
Re-running skips existing equipment codes; it does not reset reservations or reactivate deleted items.

If PostgreSQL is installed elsewhere:

```powershell
.\scripts\Populate-Equipment.ps1 -PsqlPath 'C:\Program Files\PostgreSQL\17\bin\psql.exe'
```

Alternatively, open `Data/sample-equipment.sql` in pgAdmin's Query Tool, select the
`equipment_management` database, and execute it. It is the same transaction used by the script.

Check inserted data using Swagger at `http://localhost:5080/swagger`:

```http
GET /api/equipment?search=SAMPLE-&page=1&pageSize=100
GET /api/equipment?isCentralWarehouse=true&availability=available&pageSize=100
```

To populate your own equipment one item at a time, first get valid IDs from
`GET /api/reference/equipment-types`, `GET /api/reference/locations`, and `GET /api/reference/users`.
Then call `POST http://localhost:5080/api/equipment` with `Content-Type: application/json`:

```json
{
  "equipmentCode": "VENT-CUSTOM-001",
  "name": "Warehouse Ventilator",
  "equipmentTypeId": 1,
  "locationId": 3,
  "serialNumber": "CUSTOM-SN-001",
  "manufacturer": "Your manufacturer",
  "modelNumber": "Your model",
  "purchaseDate": "2026-09-14",
  "createdBy": 1
}
```

Use a unique equipment code for each item and replace the example IDs with the reference values.
For real inventory, use the API so the equipment service validates the supplied fields and references.
New active equipment with no active allocation is available automatically; do not set a reservation
status when creating equipment. Only items at an active central warehouse location qualify for backup allocation.
