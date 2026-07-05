using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notisight.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteDurationSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "notes",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "notes");
        }
    }
}
