using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountBlocks",
                columns: table => new
                {
                    BlockerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BlockerSnapshotUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BlockedSnapshotUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBlocks", x => new { x.BlockerId, x.BlockedId });
                    table.ForeignKey(
                        name: "FK_AccountBlocks_Accounts_BlockedId",
                        column: x => x.BlockedId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId");
                    table.ForeignKey(
                        name: "FK_AccountBlocks_Accounts_BlockerId",
                        column: x => x.BlockerId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBlocks_Blocked_CreatedAt",
                table: "AccountBlocks",
                columns: new[] { "BlockedId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBlocks_Blocker_CreatedAt",
                table: "AccountBlocks",
                columns: new[] { "BlockerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountBlocks");
        }
    }
}
