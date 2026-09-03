using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivityEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PsaConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_MspOrganizationId_ActorUserId_OccurredAt",
                table: "activity_events",
                columns: new[] { "MspOrganizationId", "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_MspOrganizationId_ClientCompanyId_OccurredAt",
                table: "activity_events",
                columns: new[] { "MspOrganizationId", "ClientCompanyId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_MspOrganizationId_OccurredAt",
                table: "activity_events",
                columns: new[] { "MspOrganizationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_TicketId",
                table: "activity_events",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_events");
        }
    }
}
