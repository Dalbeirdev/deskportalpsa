using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Desk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFieldsAndPermissionTemplateEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActiveAt",
                table: "app_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "app_users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                table: "app_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "app_users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoStorageKey",
                table: "app_users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "app_users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_users_ManagerId",
                table: "app_users",
                column: "ManagerId");

            migrationBuilder.AddForeignKey(
                name: "FK_app_users_app_users_ManagerId",
                table: "app_users",
                column: "ManagerId",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_app_users_app_users_ManagerId",
                table: "app_users");

            migrationBuilder.DropIndex(
                name: "IX_app_users_ManagerId",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "LastActiveAt",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "PhotoStorageKey",
                table: "app_users");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "app_users");
        }
    }
}
