using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ControlPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_access_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Section = table.Column<int>(type: "integer", nullable: false),
                    ClientCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_access_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_access_grants_client_users_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "client_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_instructions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    LastEditedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_instructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_instructions_client_companies_ClientCompanyId",
                        column: x => x.ClientCompanyId,
                        principalTable: "client_companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_access_grants_ClientUserId_Section_ClientCompanyId",
                table: "client_access_grants",
                columns: new[] { "ClientUserId", "Section", "ClientCompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_instructions_ClientCompanyId",
                table: "ticket_instructions",
                column: "ClientCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_instructions_MspOrganizationId_ClientCompanyId",
                table: "ticket_instructions",
                columns: new[] { "MspOrganizationId", "ClientCompanyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_access_grants");

            migrationBuilder.DropTable(
                name: "ticket_instructions");
        }
    }
}
