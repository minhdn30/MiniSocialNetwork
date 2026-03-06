using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefineIndexCoverageForFeedAndExternalLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_Feed",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_ExternalLogins_AccountId",
                table: "ExternalLogins");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Feed",
                table: "Posts",
                columns: new[] { "IsDeleted", "Privacy", "CreatedAt", "PostId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_Feed",
                table: "Posts");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Feed",
                table: "Posts",
                columns: new[] { "IsDeleted", "Privacy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogins_AccountId",
                table: "ExternalLogins",
                column: "AccountId");
        }
    }
}
