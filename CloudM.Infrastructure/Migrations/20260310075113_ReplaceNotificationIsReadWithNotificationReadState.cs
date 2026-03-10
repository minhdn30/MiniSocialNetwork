using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceNotificationIsReadWithNotificationReadState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_Recipient_IsRead_LastEventAt_NotificationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Notifications");

            migrationBuilder.CreateTable(
                name: "NotificationReadStates",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastNotificationsSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFollowRequestsSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReadStates", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_NotificationReadStates_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationReadStates");

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Recipient_IsRead_LastEventAt_NotificationId",
                table: "Notifications",
                columns: new[] { "RecipientId", "IsRead", "LastEventAt", "NotificationId" });
        }
    }
}
