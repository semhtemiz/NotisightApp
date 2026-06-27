using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notisight.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteVectorSyncStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VectorSyncError",
                table: "notes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VectorSyncStatus",
                table: "notes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VectorSyncedAtUtc",
                table: "notes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notes_VectorSyncStatus",
                table: "notes",
                column: "VectorSyncStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notes_VectorSyncStatus",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "VectorSyncError",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "VectorSyncStatus",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "VectorSyncedAtUtc",
                table: "notes");
        }
    }
}
