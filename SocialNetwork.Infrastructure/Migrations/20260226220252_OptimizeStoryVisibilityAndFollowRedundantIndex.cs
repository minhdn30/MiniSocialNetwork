using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeStoryVisibilityAndFollowRedundantIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Follow_Follower_Followed",
                table: "Follows");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Privacy_Active",
                table: "Stories",
                columns: new[] { "Privacy", "ExpiresAt", "CreatedAt" },
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stories_Privacy_Active",
                table: "Stories");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_Follower_Followed",
                table: "Follows",
                columns: new[] { "FollowerId", "FollowedId" });
        }
    }
}
