using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TicketTimeProvenanceAndSyncState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedTechnicianName",
                table: "tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "ticket_attachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImportedFromProvider",
                table: "ticket_attachments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PushedToProviderAt",
                table: "ticket_attachments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TicketNoteId",
                table: "ticket_attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoImportNewTickets",
                table: "psa_connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DefaultIssueType",
                table: "psa_connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultQueueOrBoardId",
                table: "psa_connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultSubIssueType",
                table: "psa_connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultTicketType",
                table: "psa_connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultTimeEntryResourceId",
                table: "psa_connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultTimeEntryRoleId",
                table: "psa_connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FilterActiveWithinDays",
                table: "psa_connections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterCompanyIds",
                table: "psa_connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterQueueIds",
                table: "psa_connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterResourceIds",
                table: "psa_connections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImportClosedTickets",
                table: "psa_connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImportNotes",
                table: "psa_connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImportOpenTickets",
                table: "psa_connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImportSystemNotes",
                table: "psa_connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SyncAttachments",
                table: "psa_connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TwoWaySync",
                table: "psa_connections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ticket_time_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalEntryId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Hours = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    Billable = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    WorkTypeId = table.Column<string>(type: "text", nullable: true),
                    WorkTypeLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WorkRoleId = table.Column<string>(type: "text", nullable: true),
                    TechnicianExternalId = table.Column<string>(type: "text", nullable: true),
                    TechnicianName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SyncStatus = table.Column<int>(type: "integer", nullable: false),
                    SyncError = table.Column<string>(type: "text", nullable: true),
                    EntryDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_time_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_time_entries_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_attachments_TicketNoteId",
                table: "ticket_attachments",
                column: "TicketNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_time_entries_ExternalEntryId",
                table: "ticket_time_entries",
                column: "ExternalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_time_entries_TicketId",
                table: "ticket_time_entries",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_time_entries");

            migrationBuilder.DropIndex(
                name: "IX_ticket_attachments_TicketNoteId",
                table: "ticket_attachments");

            migrationBuilder.DropColumn(
                name: "AssignedTechnicianName",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "AuthorName",
                table: "ticket_attachments");

            migrationBuilder.DropColumn(
                name: "ImportedFromProvider",
                table: "ticket_attachments");

            migrationBuilder.DropColumn(
                name: "PushedToProviderAt",
                table: "ticket_attachments");

            migrationBuilder.DropColumn(
                name: "TicketNoteId",
                table: "ticket_attachments");

            migrationBuilder.DropColumn(
                name: "AutoImportNewTickets",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "DefaultIssueType",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "DefaultQueueOrBoardId",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "DefaultSubIssueType",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "DefaultTicketType",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "DefaultTimeEntryResourceId",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "DefaultTimeEntryRoleId",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "FilterActiveWithinDays",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "FilterCompanyIds",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "FilterQueueIds",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "FilterResourceIds",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "ImportClosedTickets",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "ImportNotes",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "ImportOpenTickets",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "ImportSystemNotes",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "SyncAttachments",
                table: "psa_connections");

            migrationBuilder.DropColumn(
                name: "TwoWaySync",
                table: "psa_connections");
        }
    }
}
