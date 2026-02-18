using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeChatNPlusOneAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MessageMedia_Type_Created_Message",
                table: "MessageMedias",
                columns: new[] { "MediaType", "CreatedAt", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMember_Conversation_HasLeft_Account",
                table: "ConversationMembers",
                columns: new[] { "ConversationId", "HasLeft", "AccountId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageMedia_Type_Created_Message",
                table: "MessageMedias");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMember_Conversation_HasLeft_Account",
                table: "ConversationMembers");
        }
    }
}
