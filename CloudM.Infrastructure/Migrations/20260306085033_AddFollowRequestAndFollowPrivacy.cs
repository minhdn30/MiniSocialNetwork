using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowRequestAndFollowPrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FollowPrivacy",
                table: "AccountSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FollowRequests",
                columns: table => new
                {
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowRequests", x => new { x.RequesterId, x.TargetId });
                    table.ForeignKey(
                        name: "FK_FollowRequests_Accounts_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId");
                    table.ForeignKey(
                        name: "FK_FollowRequests_Accounts_TargetId",
                        column: x => x.TargetId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FollowRequests_RequesterId_CreatedAt",
                table: "FollowRequests",
                columns: new[] { "RequesterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FollowRequests_TargetId_CreatedAt",
                table: "FollowRequests",
                columns: new[] { "TargetId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FollowRequests");

            migrationBuilder.DropColumn(
                name: "FollowPrivacy",
                table: "AccountSettings");
        }
    }
}
