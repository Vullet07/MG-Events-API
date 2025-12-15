using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class typo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ForumPosts_ForumThread_ThreadId",
                table: "ForumPosts");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumThread_Users_CreatedByUserId",
                table: "ForumThread");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ForumThread",
                table: "ForumThread");

            migrationBuilder.RenameTable(
                name: "ForumThread",
                newName: "ForumThreads");

            migrationBuilder.RenameIndex(
                name: "IX_ForumThread_CreatedByUserId",
                table: "ForumThreads",
                newName: "IX_ForumThreads_CreatedByUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ForumThreads",
                table: "ForumThreads",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumPosts_ForumThreads_ThreadId",
                table: "ForumPosts",
                column: "ThreadId",
                principalTable: "ForumThreads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ForumThreads_Users_CreatedByUserId",
                table: "ForumThreads",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ForumPosts_ForumThreads_ThreadId",
                table: "ForumPosts");

            migrationBuilder.DropForeignKey(
                name: "FK_ForumThreads_Users_CreatedByUserId",
                table: "ForumThreads");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ForumThreads",
                table: "ForumThreads");

            migrationBuilder.RenameTable(
                name: "ForumThreads",
                newName: "ForumThread");

            migrationBuilder.RenameIndex(
                name: "IX_ForumThreads_CreatedByUserId",
                table: "ForumThread",
                newName: "IX_ForumThread_CreatedByUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ForumThread",
                table: "ForumThread",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ForumPosts_ForumThread_ThreadId",
                table: "ForumPosts",
                column: "ThreadId",
                principalTable: "ForumThread",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ForumThread_Users_CreatedByUserId",
                table: "ForumThread",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
