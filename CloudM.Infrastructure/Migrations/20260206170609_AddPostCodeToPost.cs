using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostCodeToPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_Account_CreatedAt",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMembers_ConversationId",
                table: "ConversationMembers");

            migrationBuilder.AddColumn<string>(
                name: "PostCode",
                table: "Posts",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            // Initial manual update to avoid unique constraint violation if empty
            migrationBuilder.Sql(@"
                UPDATE ""Posts""
                SET ""PostCode"" = LEFT(MD5(RANDOM()::TEXT), 10)
                WHERE ""PostCode"" = '';
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Account_CreatedAt",
                table: "Posts",
                columns: new[] { "AccountId", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_PostCode",
                table: "Posts",
                column: "PostCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_Account_CreatedAt",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_PostCode",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "PostCode",
                table: "Posts");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Account_CreatedAt",
                table: "Posts",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembers_ConversationId",
                table: "ConversationMembers",
                column: "ConversationId");
        }
    }
}
