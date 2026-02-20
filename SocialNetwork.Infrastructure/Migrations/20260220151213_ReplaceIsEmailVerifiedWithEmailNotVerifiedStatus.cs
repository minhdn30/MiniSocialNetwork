using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIsEmailVerifiedWithEmailNotVerifiedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Accounts"
                SET "Status" = 5
                WHERE "IsEmailVerified" = FALSE
                  AND "Status" IN (0, 1);
                """);

            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                table: "Accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "Accounts"
                SET "IsEmailVerified" = CASE
                    WHEN "Status" = 5 THEN FALSE
                    ELSE TRUE
                END;
                """);

            migrationBuilder.Sql("""
                UPDATE "Accounts"
                SET "Status" = 0
                WHERE "Status" = 5;
                """);
        }
    }
}
