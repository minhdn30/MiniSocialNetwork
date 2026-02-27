using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeChatQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Message_Conversation_Account_SentAt",
                table: "Messages",
                columns: new[] { "ConversationId", "AccountId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Name_Trgm",
                table: "Conversations",
                column: "ConversationName")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMember_Account_State_Conversation",
                table: "ConversationMembers",
                columns: new[] { "AccountId", "HasLeft", "IsMuted", "ConversationId" });

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Accounts_Username_Trgm""
                ON ""Accounts"" USING GIN (""Username"" gin_trgm_ops);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Message_Conversation_Account_SentAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_Name_Trgm",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMember_Account_State_Conversation",
                table: "ConversationMembers");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Accounts_Username_Trgm"";");
        }
    }
}
