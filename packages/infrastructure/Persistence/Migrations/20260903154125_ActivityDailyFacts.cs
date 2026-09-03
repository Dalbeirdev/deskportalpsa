using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivityDailyFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_daily_facts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ActorExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventCount = table.Column<int>(type: "integer", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_daily_facts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_daily_facts_MspOrganizationId_ActorExternalId_Day",
                table: "activity_daily_facts",
                columns: new[] { "MspOrganizationId", "ActorExternalId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_daily_facts_MspOrganizationId_ClientCompanyId_Day",
                table: "activity_daily_facts",
                columns: new[] { "MspOrganizationId", "ClientCompanyId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_daily_facts_MspOrganizationId_Day",
                table: "activity_daily_facts",
                columns: new[] { "MspOrganizationId", "Day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_daily_facts");
        }
    }
}
