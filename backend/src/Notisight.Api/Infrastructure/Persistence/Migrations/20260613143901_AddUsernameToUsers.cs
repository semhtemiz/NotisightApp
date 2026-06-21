using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notisight.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "users",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE users
                SET Username = LOWER(
                    LEFT(
                        REPLACE(REPLACE(REPLACE(SUBSTRING(Email, 1, CHARINDEX('@', Email + '@') - 1), '.', '_'), '+', '_'), '-', '_')
                        + '_' + LEFT(REPLACE(CONVERT(nvarchar(36), Id), '-', ''), 8),
                        60
                    )
                )
                WHERE Username IS NULL OR Username = ''
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "users",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_Username",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "users");
        }
    }
}
