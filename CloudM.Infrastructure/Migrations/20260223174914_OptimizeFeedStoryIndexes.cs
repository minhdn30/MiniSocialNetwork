using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeFeedStoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StoryViews_Viewer_Story",
                table: "StoryViews",
                columns: new[] { "ViewerAccountId", "StoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Post_Account_Created",
                table: "Comments",
                columns: new[] { "PostId", "AccountId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StoryViews_Viewer_Story",
                table: "StoryViews");

            migrationBuilder.DropIndex(
                name: "IX_Comment_Post_Account_Created",
                table: "Comments");
        }
    }
}
