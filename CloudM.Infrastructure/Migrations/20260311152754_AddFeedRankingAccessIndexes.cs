using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedRankingAccessIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostReacts_AccountId",
                table: "PostReacts");

            migrationBuilder.CreateIndex(
                name: "IX_PostReacts_Account_CreatedAt_PostId",
                table: "PostReacts",
                columns: new[] { "AccountId", "CreatedAt", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_PostReacts_Post_CreatedAt",
                table: "PostReacts",
                columns: new[] { "PostId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Account_CreatedAt_PostId",
                table: "Comments",
                columns: new[] { "AccountId", "CreatedAt", "PostId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostReacts_Account_CreatedAt_PostId",
                table: "PostReacts");

            migrationBuilder.DropIndex(
                name: "IX_PostReacts_Post_CreatedAt",
                table: "PostReacts");

            migrationBuilder.DropIndex(
                name: "IX_Comment_Account_CreatedAt_PostId",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_PostReacts_AccountId",
                table: "PostReacts",
                column: "AccountId");
        }
    }
}
