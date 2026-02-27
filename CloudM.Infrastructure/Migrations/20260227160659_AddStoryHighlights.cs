using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryHighlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoryHighlightGroups",
                columns: table => new
                {
                    StoryHighlightGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CoverImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryHighlightGroups", x => x.StoryHighlightGroupId);
                    table.CheckConstraint("CK_StoryHighlightGroups_Name_NotEmpty", "length(btrim(\"Name\")) > 0");
                    table.ForeignKey(
                        name: "FK_StoryHighlightGroups_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryHighlightItems",
                columns: table => new
                {
                    StoryHighlightGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryHighlightItems", x => new { x.StoryHighlightGroupId, x.StoryId });
                    table.ForeignKey(
                        name: "FK_StoryHighlightItems_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "StoryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoryHighlightItems_StoryHighlightGroups_StoryHighlightGrou~",
                        column: x => x.StoryHighlightGroupId,
                        principalTable: "StoryHighlightGroups",
                        principalColumn: "StoryHighlightGroupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoryHighlightGroups_Account_CreatedAt",
                table: "StoryHighlightGroups",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryHighlightItems_Group_AddedAt",
                table: "StoryHighlightItems",
                columns: new[] { "StoryHighlightGroupId", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryHighlightItems_StoryId",
                table: "StoryHighlightItems",
                column: "StoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryHighlightItems");

            migrationBuilder.DropTable(
                name: "StoryHighlightGroups");
        }
    }
}
