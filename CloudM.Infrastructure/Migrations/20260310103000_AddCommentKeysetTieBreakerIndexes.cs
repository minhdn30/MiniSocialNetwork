using CloudM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260310103000_AddCommentKeysetTieBreakerIndexes")]
    public partial class AddCommentKeysetTieBreakerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comment_Parent_Created",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comment_Post_Parent_Created",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Parent_Created",
                table: "Comments",
                columns: new[] { "ParentCommentId", "CreatedAt", "CommentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Post_Parent_Created",
                table: "Comments",
                columns: new[] { "PostId", "ParentCommentId", "CreatedAt", "CommentId" });
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
                name: "IX_Comment_Parent_Created",
                table: "Comments",
                columns: new[] { "ParentCommentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Comment_Post_Parent_Created",
                table: "Comments",
                columns: new[] { "PostId", "ParentCommentId", "CreatedAt" });
        }
    }
}
