using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class EmailConfirmationGenderAndResolvedPins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailConfirmationTokenExpiresAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailConfirmationTokenHash",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailConfirmedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailConfirmed",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailConfirmationTokenExpiresAt",
                table: "TeacherRegistrationRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailConfirmationTokenHash",
                table: "TeacherRegistrationRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailConfirmedAt",
                table: "TeacherRegistrationRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gender",
                table: "TeacherRegistrationRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailConfirmed",
                table: "TeacherRegistrationRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE [Users]
                SET
                    [IsEmailConfirmed] = 1,
                    [EmailConfirmedAt] = COALESCE([EmailConfirmedAt], SYSUTCDATETIME()),
                    [EmailConfirmationTokenHash] = NULL,
                    [EmailConfirmationTokenExpiresAt] = NULL
                WHERE [IsDeleted] = 0;
                """);

            migrationBuilder.Sql("""
                UPDATE [TeacherRegistrationRequests]
                SET
                    [IsEmailConfirmed] = 1,
                    [EmailConfirmedAt] = COALESCE([EmailConfirmedAt], SYSUTCDATETIME()),
                    [EmailConfirmationTokenHash] = NULL,
                    [EmailConfirmationTokenExpiresAt] = NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "EventPins",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "EventPins",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsResolved",
                table: "EventPins",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "EventPins",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolvedByUserId",
                table: "EventPins",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailConfirmationTokenHash",
                table: "Users",
                column: "EmailConfirmationTokenHash",
                unique: true,
                filter: "[EmailConfirmationTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherRegistrationRequests_EmailConfirmationTokenHash",
                table: "TeacherRegistrationRequests",
                column: "EmailConfirmationTokenHash",
                unique: true,
                filter: "[EmailConfirmationTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventPins_ArchivedAt",
                table: "EventPins",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EventPins_IsResolved",
                table: "EventPins",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_EventPins_ResolvedByUserId",
                table: "EventPins",
                column: "ResolvedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_EventPins_Users_ResolvedByUserId",
                table: "EventPins",
                column: "ResolvedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventPins_Users_ResolvedByUserId",
                table: "EventPins");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmailConfirmationTokenHash",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TeacherRegistrationRequests_EmailConfirmationTokenHash",
                table: "TeacherRegistrationRequests");

            migrationBuilder.DropIndex(
                name: "IX_EventPins_ArchivedAt",
                table: "EventPins");

            migrationBuilder.DropIndex(
                name: "IX_EventPins_IsResolved",
                table: "EventPins");

            migrationBuilder.DropIndex(
                name: "IX_EventPins_ResolvedByUserId",
                table: "EventPins");

            migrationBuilder.DropColumn(
                name: "EmailConfirmationTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailConfirmationTokenHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailConfirmedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsEmailConfirmed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailConfirmationTokenExpiresAt",
                table: "TeacherRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "EmailConfirmationTokenHash",
                table: "TeacherRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "EmailConfirmedAt",
                table: "TeacherRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "TeacherRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "IsEmailConfirmed",
                table: "TeacherRegistrationRequests");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "EventPins");

            migrationBuilder.DropColumn(
                name: "IsResolved",
                table: "EventPins");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "EventPins");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "EventPins");

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "EventPins",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
