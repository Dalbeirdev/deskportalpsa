using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationAndAuthorizationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "role_permissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "permission_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BaseRoleType = table.Column<int>(type: "integer", nullable: false),
                    IsSystemTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_board_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_board_access", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_board_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PsaConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Actions = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_board_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_board_grants_psa_connections_PsaConnectionId",
                        column: x => x.PsaConnectionId,
                        principalTable: "psa_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_permission_overrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Effect = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AppliedFromTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permission_overrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_teams_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_departments_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "permission_template_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Effect = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission_template_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_permission_template_entries_permission_templates_Permission~",
                        column: x => x.PermissionTemplateId,
                        principalTable: "permission_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MspOrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_teams_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_departments_MspOrganizationId_Name",
                table: "departments",
                columns: new[] { "MspOrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permission_template_entries_PermissionTemplateId_Permission~",
                table: "permission_template_entries",
                columns: new[] { "PermissionTemplateId", "PermissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permission_templates_MspOrganizationId_Name",
                table: "permission_templates",
                columns: new[] { "MspOrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teams_DepartmentId_Name",
                table: "teams",
                columns: new[] { "DepartmentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_board_access_AppUserId",
                table: "user_board_access",
                column: "AppUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_board_grants_AppUserId_PsaConnectionId_BoardName",
                table: "user_board_grants",
                columns: new[] { "AppUserId", "PsaConnectionId", "BoardName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_board_grants_PsaConnectionId",
                table: "user_board_grants",
                column: "PsaConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_departments_AppUserId",
                table: "user_departments",
                column: "AppUserId",
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_user_departments_AppUserId_DepartmentId",
                table: "user_departments",
                columns: new[] { "AppUserId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_departments_DepartmentId",
                table: "user_departments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permission_overrides_AppUserId_PermissionKey",
                table: "user_permission_overrides",
                columns: new[] { "AppUserId", "PermissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_teams_AppUserId_TeamId",
                table: "user_teams",
                columns: new[] { "AppUserId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_teams_TeamId",
                table: "user_teams",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permission_template_entries");

            migrationBuilder.DropTable(
                name: "user_board_access");

            migrationBuilder.DropTable(
                name: "user_board_grants");

            migrationBuilder.DropTable(
                name: "user_departments");

            migrationBuilder.DropTable(
                name: "user_permission_overrides");

            migrationBuilder.DropTable(
                name: "user_teams");

            migrationBuilder.DropTable(
                name: "permission_templates");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "role_permissions");
        }
    }
}
