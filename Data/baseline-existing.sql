-- Only for a database created by the ORIGINAL application's EnsureCreated, before migrations.
-- Stop the application, back up the database, then execute this script once.
-- This records the existing schema; it does not recreate tables or remove application data.
BEGIN;
DO $$
DECLARE required_table text;
BEGIN
    FOREACH required_table IN ARRAY ARRAY['users','locations','equipment_types','equipment',
        'equipment_technicians','equipment_approvers','equipment_calibration_rule','tickets',
        'ticket_history','ticket_documents','maintenance_schedules','backup_requests',
        'backup_allocations','backup_escalations','notifications','audit_logs']
    LOOP
        IF to_regclass('public.' || required_table) IS NULL THEN
            RAISE EXCEPTION 'Missing original table %. Do not baseline a new or partial database.', required_table;
        END IF;
    END LOOP;
END $$;
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" varchar(150) NOT NULL PRIMARY KEY,
    "ProductVersion" varchar(32) NOT NULL
);
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260914160344_InitialSchema', '8.0.31') ON CONFLICT DO NOTHING;
COMMIT;
