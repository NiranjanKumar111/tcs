using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EquipmentManagementBackend.Migrations
{
    /// <inheritdoc />
    public partial class ConsoleComplianceWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "users",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TicketType",
                table: "tickets",
                type: "text",
                nullable: false,
                defaultValue: "corrective");

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkCompletedOn",
                table: "tickets",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkReport",
                table: "tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Building",
                table: "locations",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Floor",
                table: "locations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Room",
                table: "locations",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shelf",
                table: "locations",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ward",
                table: "locations",
                type: "text",
                nullable: false,
                defaultValue: "OTHER");

            migrationBuilder.AddColumn<DateOnly>(
                name: "CalibrationAnchorDate",
                table: "equipment",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalibrationFrequency",
                table: "equipment",
                type: "text",
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastCalibrationDate",
                table: "equipment",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastMaintenanceDate",
                table: "equipment",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MaintenanceAnchorDate",
                table: "equipment",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceFrequency",
                table: "equipment",
                type: "text",
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<DateOnly>(
                name: "NextCalibrationDate",
                table: "equipment",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NextMaintenanceDate",
                table: "equipment",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ticket_calibration_results",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    Parameter = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Minimum = table.Column<decimal>(type: "numeric", nullable: false),
                    Maximum = table.Column<decimal>(type: "numeric", nullable: false),
                    Measured = table.Column<decimal>(type: "numeric", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_calibration_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_calibration_results_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_compliance_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_compliance_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_compliance_items_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_compliance_verifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<long>(type: "bigint", nullable: false),
                    ApproverId = table.Column<long>(type: "bigint", nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    TicketVersion = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_compliance_verifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_compliance_verifications_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_compliance_verifications_users_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tickets_TicketType_Status_CreatedAt",
                table: "tickets",
                columns: new[] { "TicketType", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_equipment_NextCalibrationDate",
                table: "equipment",
                column: "NextCalibrationDate");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_NextMaintenanceDate",
                table: "equipment",
                column: "NextMaintenanceDate");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_calibration_results_TicketId",
                table: "ticket_calibration_results",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_compliance_items_TicketId",
                table: "ticket_compliance_items",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_compliance_verifications_ApproverId",
                table: "ticket_compliance_verifications",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_compliance_verifications_TicketId",
                table: "ticket_compliance_verifications",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_calibration_results");

            migrationBuilder.DropTable(
                name: "ticket_compliance_items");

            migrationBuilder.DropTable(
                name: "ticket_compliance_verifications");

            migrationBuilder.DropIndex(
                name: "IX_tickets_TicketType_Status_CreatedAt",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_equipment_NextCalibrationDate",
                table: "equipment");

            migrationBuilder.DropIndex(
                name: "IX_equipment_NextMaintenanceDate",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "TicketType",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "WorkCompletedOn",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "WorkReport",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "Building",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "Room",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "Shelf",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "Ward",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "CalibrationAnchorDate",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "CalibrationFrequency",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "LastCalibrationDate",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "LastMaintenanceDate",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "MaintenanceAnchorDate",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "MaintenanceFrequency",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "NextCalibrationDate",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "NextMaintenanceDate",
                table: "equipment");
        }
    }
}
