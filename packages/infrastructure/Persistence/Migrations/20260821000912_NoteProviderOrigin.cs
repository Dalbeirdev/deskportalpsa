using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NoteProviderOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ImportedFromProvider",
                table: "ticket_notes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: until now, "not client-authored + has a provider id" was exactly the set of
            // rows deletion reconciliation treated as provider-origin. Stamp them so that behavior
            // carries over unchanged; client portal replies (AuthoredByClient) stay unstamped and
            // therefore protected, exactly as before.
            migrationBuilder.Sql(
                """UPDATE ticket_notes SET "ImportedFromProvider" = TRUE WHERE "ExternalNoteId" IS NOT NULL AND NOT "AuthoredByClient";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImportedFromProvider",
                table: "ticket_notes");
        }
    }
}
