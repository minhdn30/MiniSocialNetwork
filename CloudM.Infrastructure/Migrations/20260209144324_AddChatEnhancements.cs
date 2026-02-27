using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "Messages",
                newName: "IsRecalled");

            migrationBuilder.AddColumn<DateTime>(
                name: "RecalledAt",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemMessageDataJson",
                table: "Messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "ConversationMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MessageHiddens",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    HiddenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageHiddens", x => new { x.MessageId, x.AccountId });
                    table.ForeignKey(
                        name: "FK_MessageHiddens_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageHiddens_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageReacts",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReactType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReacts", x => new { x.MessageId, x.AccountId });
                    table.ForeignKey(
                        name: "FK_MessageReacts_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageReacts_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageHiddens_AccountId",
                table: "MessageHiddens",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReacts_AccountId",
                table: "MessageReacts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReacts_MessageId",
                table: "MessageReacts",
                column: "MessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageHiddens");

            migrationBuilder.DropTable(
                name: "MessageReacts");

            migrationBuilder.DropColumn(
                name: "RecalledAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SystemMessageDataJson",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "ConversationMembers");

            migrationBuilder.RenameColumn(
                name: "IsRecalled",
                table: "Messages",
                newName: "IsDeleted");
        }
    }
}
