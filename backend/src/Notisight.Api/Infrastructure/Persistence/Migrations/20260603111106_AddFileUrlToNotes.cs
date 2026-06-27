using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notisight.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFileUrlToNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "notes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileUrl",
                table: "notes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileType",
                table: "notes");

            migrationBuilder.DropColumn(
                name: "FileUrl",
                table: "notes");
        }
    }
}
