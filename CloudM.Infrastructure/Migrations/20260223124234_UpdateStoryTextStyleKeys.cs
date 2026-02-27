using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStoryTextStyleKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stories_ContentPayload",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "Stories");

            migrationBuilder.AddColumn<string>(
                name: "BackgroundColorKey",
                table: "Stories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FontSizeKey",
                table: "Stories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FontTextKey",
                table: "Stories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextColorKey",
                table: "Stories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stories_ContentPayload",
                table: "Stories",
                sql: "((\"ContentType\" IN (0,1) AND \"MediaUrl\" IS NOT NULL AND \"TextContent\" IS NULL AND \"BackgroundColorKey\" IS NULL AND \"FontTextKey\" IS NULL AND \"FontSizeKey\" IS NULL AND \"TextColorKey\" IS NULL) OR (\"ContentType\" = 2 AND \"TextContent\" IS NOT NULL AND length(btrim(\"TextContent\")) > 0 AND \"MediaUrl\" IS NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stories_ContentPayload",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "BackgroundColorKey",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "FontSizeKey",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "FontTextKey",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "TextColorKey",
                table: "Stories");

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "Stories",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stories_ContentPayload",
                table: "Stories",
                sql: "((\"ContentType\" IN (0,1) AND \"MediaUrl\" IS NOT NULL AND \"TextContent\" IS NULL) OR (\"ContentType\" = 2 AND \"TextContent\" IS NOT NULL AND length(btrim(\"TextContent\")) > 0 AND \"MediaUrl\" IS NULL))");
        }
    }
}
