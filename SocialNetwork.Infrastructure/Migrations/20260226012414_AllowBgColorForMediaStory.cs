using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowBgColorForMediaStory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stories_ContentPayload",
                table: "Stories");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stories_ContentPayload",
                table: "Stories",
                sql: "((\"ContentType\" IN (0,1) AND \"MediaUrl\" IS NOT NULL AND \"TextContent\" IS NULL AND \"FontTextKey\" IS NULL AND \"FontSizeKey\" IS NULL AND \"TextColorKey\" IS NULL) OR (\"ContentType\" = 2 AND \"TextContent\" IS NOT NULL AND length(btrim(\"TextContent\")) > 0 AND \"MediaUrl\" IS NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stories_ContentPayload",
                table: "Stories");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stories_ContentPayload",
                table: "Stories",
                sql: "((\"ContentType\" IN (0,1) AND \"MediaUrl\" IS NOT NULL AND \"TextContent\" IS NULL AND \"BackgroundColorKey\" IS NULL AND \"FontTextKey\" IS NULL AND \"FontSizeKey\" IS NULL AND \"TextColorKey\" IS NULL) OR (\"ContentType\" = 2 AND \"TextContent\" IS NOT NULL AND length(btrim(\"TextContent\")) > 0 AND \"MediaUrl\" IS NULL))");
        }
    }
}
