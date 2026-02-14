using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PinnedMessages",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PinnedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PinnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PinnedMessages", x => new { x.ConversationId, x.MessageId });
                    table.ForeignKey(
                        name: "FK_PinnedMessages_Accounts_PinnedBy",
                        column: x => x.PinnedBy,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PinnedMessages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PinnedMessages_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PinnedMessage_Conversation_PinnedAt",
                table: "PinnedMessages",
                columns: new[] { "ConversationId", "PinnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PinnedMessages_MessageId",
                table: "PinnedMessages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedMessages_PinnedBy",
                table: "PinnedMessages",
                column: "PinnedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PinnedMessages");
        }
    }
}
