using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_tickets_ClientCompanyId",
                table: "tickets",
                column: "ClientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_MspOrganizationId_AssignedTechnicianExternalId",
                table: "tickets",
                columns: new[] { "MspOrganizationId", "AssignedTechnicianExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tickets_ClientCompanyId",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_tickets_MspOrganizationId_AssignedTechnicianExternalId",
                table: "tickets");
        }
    }
}
