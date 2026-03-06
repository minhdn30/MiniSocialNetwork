using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeMessageReactIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageReacts_MessageId",
                table: "MessageReacts");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReacts_MessageId_ReactType",
                table: "MessageReacts",
                columns: new[] { "MessageId", "ReactType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageReacts_MessageId_ReactType",
                table: "MessageReacts");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReacts_MessageId",
                table: "MessageReacts",
                column: "MessageId");
        }
    }
}
