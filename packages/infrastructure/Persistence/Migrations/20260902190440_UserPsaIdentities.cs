using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserPsaIdentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_psa_identities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PsaConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalTechnicianId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalTechnicianName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_psa_identities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_psa_identities_app_users_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_psa_identities_psa_connections_PsaConnectionId",
                        column: x => x.PsaConnectionId,
                        principalTable: "psa_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_psa_identities_AppUserId_PsaConnectionId",
                table: "user_psa_identities",
                columns: new[] { "AppUserId", "PsaConnectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_psa_identities_PsaConnectionId",
                table: "user_psa_identities",
                column: "PsaConnectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_psa_identities");
        }
    }
}
