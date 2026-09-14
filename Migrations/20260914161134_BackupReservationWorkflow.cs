using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipmentManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class BackupReservationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_backup_escalations_RequestId",
                table: "backup_escalations");

            migrationBuilder.DropIndex(
                name: "IX_backup_allocations_EquipmentId",
                table: "backup_allocations");

            migrationBuilder.DropIndex(
                name: "IX_backup_allocations_RequestId",
                table: "backup_allocations");

            migrationBuilder.AddColumn<bool>(
                name: "IsCentralWarehouse",
                table: "locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BedNumber",
                table: "backup_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<long>(
                name: "AssignedAdminId",
                table: "backup_escalations",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<DateTime>(
                name: "PickedUpAt",
                table: "backup_allocations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAt",
                table: "backup_allocations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReservationExpiresAt",
                table: "backup_allocations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedAt",
                table: "backup_allocations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "backup_allocations",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Legacy allocations have no lifecycle state. Conservatively keep non-terminal
            // equipment occupied until explicitly returned; never release it on deployment.
            migrationBuilder.Sql("""
                UPDATE backup_allocations a SET
                    "Status" = CASE WHEN r."Status" = 'returned' THEN 'returned'
                                    WHEN r."Status" = 'cancelled' THEN 'cancelled' ELSE 'in_use' END,
                    "ReservationExpiresAt" = a."AllocatedAt" + interval '20 minutes'
                FROM backup_requests r WHERE a."RequestId" = r."Id";
                UPDATE backup_requests r SET "Status" = 'in_use'
                WHERE EXISTS (SELECT 1 FROM backup_allocations a WHERE a."RequestId" = r."Id" AND a."Status" = 'in_use');
                UPDATE locations SET "IsCentralWarehouse" = TRUE WHERE "Code" = 'CENTRAL-WAREHOUSE';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_backup_escalations_RequestId",
                table: "backup_escalations",
                column: "RequestId",
                unique: true,
                filter: "\"ResolvedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_backup_allocations_EquipmentId",
                table: "backup_allocations",
                column: "EquipmentId",
                unique: true,
                filter: "\"Status\" IN ('reserved', 'in_use')");

            migrationBuilder.CreateIndex(
                name: "IX_backup_allocations_RequestId",
                table: "backup_allocations",
                column: "RequestId",
                unique: true,
                filter: "\"Status\" IN ('reserved', 'in_use')");

            migrationBuilder.CreateIndex(
                name: "IX_backup_allocations_Status_ReservationExpiresAt",
                table: "backup_allocations",
                columns: new[] { "Status", "ReservationExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_backup_escalations_RequestId",
                table: "backup_escalations");

            migrationBuilder.DropIndex(
                name: "IX_backup_allocations_EquipmentId",
                table: "backup_allocations");

            migrationBuilder.DropIndex(
                name: "IX_backup_allocations_RequestId",
                table: "backup_allocations");

            migrationBuilder.DropIndex(
                name: "IX_backup_allocations_Status_ReservationExpiresAt",
                table: "backup_allocations");

            migrationBuilder.DropColumn(
                name: "IsCentralWarehouse",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "BedNumber",
                table: "backup_requests");

            migrationBuilder.DropColumn(
                name: "PickedUpAt",
                table: "backup_allocations");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "backup_allocations");

            migrationBuilder.DropColumn(
                name: "ReservationExpiresAt",
                table: "backup_allocations");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                table: "backup_allocations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "backup_allocations");

            migrationBuilder.AlterColumn<long>(
                name: "AssignedAdminId",
                table: "backup_escalations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_backup_escalations_RequestId",
                table: "backup_escalations",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_backup_allocations_EquipmentId",
                table: "backup_allocations",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_backup_allocations_RequestId",
                table: "backup_allocations",
                column: "RequestId");
        }
    }
}
