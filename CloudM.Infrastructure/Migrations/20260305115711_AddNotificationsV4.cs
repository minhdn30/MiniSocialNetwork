using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsV4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationOutboxes",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationOutboxes", x => x.OutboxId);
                    table.ForeignKey(
                        name: "FK_NotificationOutboxes_Accounts_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    AggregateKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastEventAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorCount = table.Column<int>(type: "integer", nullable: false),
                    EventCount = table.Column<int>(type: "integer", nullable: false),
                    LastActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastActorSnapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TargetKind = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_Accounts_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationContributions",
                columns: table => new
                {
                    ContributionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationContributions", x => x.ContributionId);
                    table.ForeignKey(
                        name: "FK_NotificationContributions_Accounts_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationContributions_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "NotificationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationContributions_ActorId",
                table: "NotificationContributions",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationContributions_Notification_IsActive_UpdatedAt",
                table: "NotificationContributions",
                columns: new[] { "NotificationId", "IsActive", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationContributions_Notification_Source_Unique",
                table: "NotificationContributions",
                columns: new[] { "NotificationId", "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_LockedUntil_Status",
                table: "NotificationOutboxes",
                columns: new[] { "LockedUntil", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutbox_Status_NextRetryAt_OccurredAt",
                table: "NotificationOutboxes",
                columns: new[] { "Status", "NextRetryAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxes_RecipientId",
                table: "NotificationOutboxes",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Recipient_IsRead_LastEventAt_NotificationId",
                table: "Notifications",
                columns: new[] { "RecipientId", "IsRead", "LastEventAt", "NotificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Recipient_LastEventAt_NotificationId",
                table: "Notifications",
                columns: new[] { "RecipientId", "LastEventAt", "NotificationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Recipient_Type_Aggregate_Unique",
                table: "Notifications",
                columns: new[] { "RecipientId", "Type", "AggregateKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationContributions");

            migrationBuilder.DropTable(
                name: "NotificationOutboxes");

            migrationBuilder.DropTable(
                name: "Notifications");
        }
    }
}
