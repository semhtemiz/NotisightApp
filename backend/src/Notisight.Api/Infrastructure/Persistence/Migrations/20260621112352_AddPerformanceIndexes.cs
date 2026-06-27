using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notisight.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notes_UserId",
                table: "notes");

            migrationBuilder.DropIndex(
                name: "IX_folders_UserId",
                table: "folders");

            migrationBuilder.CreateIndex(
                name: "IX_notes_UserId_FolderId",
                table: "notes",
                columns: new[] { "UserId", "FolderId" });

            migrationBuilder.CreateIndex(
                name: "IX_notes_UserId_UpdatedAtUtc",
                table: "notes",
                columns: new[] { "UserId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_folders_UserId_ParentFolderId",
                table: "folders",
                columns: new[] { "UserId", "ParentFolderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notes_UserId_FolderId",
                table: "notes");

            migrationBuilder.DropIndex(
                name: "IX_notes_UserId_UpdatedAtUtc",
                table: "notes");

            migrationBuilder.DropIndex(
                name: "IX_folders_UserId_ParentFolderId",
                table: "folders");

            migrationBuilder.CreateIndex(
                name: "IX_notes_UserId",
                table: "notes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_folders_UserId",
                table: "folders",
                column: "UserId");
        }
    }
}
