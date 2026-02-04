using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeFollowIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Follows_FollowedId",
                table: "Follows");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FollowedId_CreatedAt",
                table: "Follows",
                columns: new[] { "FollowedId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FollowerId_CreatedAt",
                table: "Follows",
                columns: new[] { "FollowerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Follows_FollowedId_CreatedAt",
                table: "Follows");

            migrationBuilder.DropIndex(
                name: "IX_Follows_FollowerId_CreatedAt",
                table: "Follows");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FollowedId",
                table: "Follows",
                column: "FollowedId");
        }
    }
}
