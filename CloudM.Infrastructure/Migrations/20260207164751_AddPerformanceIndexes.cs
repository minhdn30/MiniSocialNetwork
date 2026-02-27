using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Comments_AccountId",
                table: "Comments",
                newName: "IX_Comment_AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReact_PostId_Covering",
                table: "PostReacts",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Follow_Follower_Followed",
                table: "Follows",
                columns: new[] { "FollowerId", "FollowedId" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Status",
                table: "Accounts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostReact_PostId_Covering",
                table: "PostReacts");

            migrationBuilder.DropIndex(
                name: "IX_Follow_Follower_Followed",
                table: "Follows");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_Status",
                table: "Accounts");

            migrationBuilder.RenameIndex(
                name: "IX_Comment_AccountId",
                table: "Comments",
                newName: "IX_Comments_AccountId");
        }
    }
}
