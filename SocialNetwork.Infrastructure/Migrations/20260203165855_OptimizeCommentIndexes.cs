using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeCommentIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comment_PostId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentCommentId",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Parent_Created",
                table: "Comments",
                columns: new[] { "ParentCommentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Post_Parent_Created",
                table: "Comments",
                columns: new[] { "PostId", "ParentCommentId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comment_Parent_Created",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comment_Post_Parent_Created",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_Comment_PostId",
                table: "Comments",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId");
        }
    }
}
