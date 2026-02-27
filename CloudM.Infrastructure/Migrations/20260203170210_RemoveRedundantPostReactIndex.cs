using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantPostReactIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostReact_PostId",
                table: "PostReacts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PostReact_PostId",
                table: "PostReacts",
                column: "PostId");
        }
    }
}
