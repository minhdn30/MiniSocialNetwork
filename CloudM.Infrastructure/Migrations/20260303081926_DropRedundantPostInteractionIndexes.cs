using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropRedundantPostInteractionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostSaves_PostId",
                table: "PostSaves");

            migrationBuilder.DropIndex(
                name: "IX_PostReact_PostId_Covering",
                table: "PostReacts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PostSaves_PostId",
                table: "PostSaves",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReact_PostId_Covering",
                table: "PostReacts",
                column: "PostId");
        }
    }
}
