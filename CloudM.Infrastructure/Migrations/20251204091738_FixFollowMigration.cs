using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixFollowMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Follow_Accounts_FollowedId",
                table: "Follow");

            migrationBuilder.DropForeignKey(
                name: "FK_Follow_Accounts_FollowerId",
                table: "Follow");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Follow",
                table: "Follow");

            migrationBuilder.RenameTable(
                name: "Follow",
                newName: "Follows");

            migrationBuilder.RenameIndex(
                name: "IX_Follow_FollowedId",
                table: "Follows",
                newName: "IX_Follows_FollowedId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Follows",
                table: "Follows",
                columns: new[] { "FollowerId", "FollowedId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Follows_Accounts_FollowedId",
                table: "Follows",
                column: "FollowedId",
                principalTable: "Accounts",
                principalColumn: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Follows_Accounts_FollowerId",
                table: "Follows",
                column: "FollowerId",
                principalTable: "Accounts",
                principalColumn: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Follows_Accounts_FollowedId",
                table: "Follows");

            migrationBuilder.DropForeignKey(
                name: "FK_Follows_Accounts_FollowerId",
                table: "Follows");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Follows",
                table: "Follows");

            migrationBuilder.RenameTable(
                name: "Follows",
                newName: "Follow");

            migrationBuilder.RenameIndex(
                name: "IX_Follows_FollowedId",
                table: "Follow",
                newName: "IX_Follow_FollowedId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Follow",
                table: "Follow",
                columns: new[] { "FollowerId", "FollowedId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Follow_Accounts_FollowedId",
                table: "Follow",
                column: "FollowedId",
                principalTable: "Accounts",
                principalColumn: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Follow_Accounts_FollowerId",
                table: "Follow",
                column: "FollowerId",
                principalTable: "Accounts",
                principalColumn: "AccountId");
        }
    }
}
