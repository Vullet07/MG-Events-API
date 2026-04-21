using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class EventPinResolveConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventPinResolveConfirmations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PinId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventPinResolveConfirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventPinResolveConfirmations_EventPins_PinId",
                        column: x => x.PinId,
                        principalTable: "EventPins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventPinResolveConfirmations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventPinResolveConfirmations_PinId",
                table: "EventPinResolveConfirmations",
                column: "PinId");

            migrationBuilder.CreateIndex(
                name: "IX_EventPinResolveConfirmations_PinId_UserId",
                table: "EventPinResolveConfirmations",
                columns: new[] { "PinId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventPinResolveConfirmations_UserId",
                table: "EventPinResolveConfirmations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventPinResolveConfirmations");
        }
    }
}
