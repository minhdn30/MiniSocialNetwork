using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeKeysetTieBreakerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_Account_CreatedAt",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_SentAt",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Account_CreatedAt_PostId",
                table: "Posts",
                columns: new[] { "AccountId", "IsDeleted", "CreatedAt", "PostId" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_SentAt_MessageId",
                table: "Messages",
                columns: new[] { "ConversationId", "SentAt", "MessageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_Account_CreatedAt_PostId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_SentAt_MessageId",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Account_CreatedAt",
                table: "Posts",
                columns: new[] { "AccountId", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_SentAt",
                table: "Messages",
                columns: new[] { "ConversationId", "SentAt" });
        }
    }
}
