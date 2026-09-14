-- Add three sample items for each standard type to the central warehouse (21 total).
-- Re-running skips existing equipment codes, including soft-deleted items.
BEGIN;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM locations WHERE "Code" = 'CENTRAL-WAREHOUSE' AND "IsActive" AND "IsCentralWarehouse") THEN
        RAISE EXCEPTION 'Active CENTRAL-WAREHOUSE location is missing. Start the API once to seed reference data.';
    END IF;
    IF (SELECT count(*) FROM equipment_types WHERE "IsActive" AND "TypeCode" IN
        ('MECH_VENT','DEFIB','PATIENT_MON','INFUSION_PUMP','SYRINGE_PUMP','DIALYSIS_MACHINE','SUCTION_MACHINE')) <> 7 THEN
        RAISE EXCEPTION 'One or more standard active equipment types are missing. Check reference data before seeding.';
    END IF;
END $$;

INSERT INTO equipment ("EquipmentCode", "Name", "EquipmentTypeId", "LocationId",
    "SerialNumber", "Manufacturer", "ModelNumber", "PurchaseDate", "IsActive",
    "CreatedBy", "CreatedAt", "UpdatedAt")
SELECT 'SAMPLE-' || t."TypeCode" || '-' || lpad(n::text, 3, '0'),
    'Sample ' || t."TypeName" || ' ' || n,
    t."Id", l."Id", 'SAMPLE-SN-' || t."TypeCode" || '-' || lpad(n::text, 3, '0'),
    'Sample Manufacturer', 'Sample Model', CURRENT_DATE, TRUE,
    (SELECT "Id" FROM users WHERE "IsActive" AND "Role" = 'admin' ORDER BY "Id" LIMIT 1),
    now(), now()
FROM equipment_types t
CROSS JOIN generate_series(1, 3) AS n
CROSS JOIN (SELECT "Id" FROM locations WHERE "Code" = 'CENTRAL-WAREHOUSE'
    AND "IsActive" AND "IsCentralWarehouse" ORDER BY "Id" LIMIT 1) l
WHERE t."IsActive" AND t."TypeCode" IN
    ('MECH_VENT','DEFIB','PATIENT_MON','INFUSION_PUMP','SYRINGE_PUMP','DIALYSIS_MACHINE','SUCTION_MACHINE')
ON CONFLICT ("EquipmentCode") DO NOTHING;

SELECT t."TypeName", t."Id" AS "EquipmentTypeId", count(*) AS "SampleItems"
FROM equipment e JOIN equipment_types t ON t."Id" = e."EquipmentTypeId"
WHERE e."EquipmentCode" LIKE 'SAMPLE-%'
GROUP BY t."Id", t."TypeName" ORDER BY t."Id";
COMMIT;
