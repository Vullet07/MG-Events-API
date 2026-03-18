using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class StudentLifecycleAndAdminModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GradeLevel",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDeletionAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolYearStart",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ScheduledDeletionAt",
                table: "Users",
                column: "ScheduledDeletionAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_ScheduledDeletionAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GradeLevel",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ScheduledDeletionAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SchoolYearStart",
                table: "Users");
        }
    }
}
