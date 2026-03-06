using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stories",
                columns: table => new
                {
                    StoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<int>(type: "integer", nullable: false),
                    MediaUrl = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    TextContent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Privacy = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stories", x => x.StoryId);
                    table.CheckConstraint("CK_Stories_ContentPayload", "((\"ContentType\" IN (0,1) AND \"MediaUrl\" IS NOT NULL AND \"TextContent\" IS NULL) OR (\"ContentType\" = 2 AND \"TextContent\" IS NOT NULL AND length(btrim(\"TextContent\")) > 0 AND \"MediaUrl\" IS NULL))");
                    table.CheckConstraint("CK_Stories_ExpiresAt", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_Stories_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryViews",
                columns: table => new
                {
                    StoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReactType = table.Column<int>(type: "integer", nullable: true),
                    ReactedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryViews", x => new { x.StoryId, x.ViewerAccountId });
                    table.CheckConstraint("CK_StoryViews_ReactPair", "((\"ReactType\" IS NULL AND \"ReactedAt\" IS NULL) OR (\"ReactType\" IS NOT NULL AND \"ReactedAt\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_StoryViews_Accounts_ViewerAccountId",
                        column: x => x.ViewerAccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoryViews_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "StoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Account_Archive",
                table: "Stories",
                columns: new[] { "AccountId", "IsDeleted", "ExpiresAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Account_Created",
                table: "Stories",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Active",
                table: "Stories",
                columns: new[] { "IsDeleted", "ExpiresAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryViews_Story_ReactType",
                table: "StoryViews",
                columns: new[] { "StoryId", "ReactType" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryViews_Story_ViewedAt",
                table: "StoryViews",
                columns: new[] { "StoryId", "ViewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryViews_Viewer_ViewedAt",
                table: "StoryViews",
                columns: new[] { "ViewerAccountId", "ViewedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryViews");

            migrationBuilder.DropTable(
                name: "Stories");
        }
    }
}
