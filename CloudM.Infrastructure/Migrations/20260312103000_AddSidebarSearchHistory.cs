using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    public partial class AddSidebarSearchHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountSearchHistories",
                columns: table => new
                {
                    CurrentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSearchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountSearchHistories", x => new { x.CurrentId, x.TargetId });
                    table.ForeignKey(
                        name: "FK_AccountSearchHistories_Accounts_CurrentId",
                        column: x => x.CurrentId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId");
                    table.ForeignKey(
                        name: "FK_AccountSearchHistories_Accounts_TargetId",
                        column: x => x.TargetId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSearchHistories_Current_LastSearchedAt",
                table: "AccountSearchHistories",
                columns: new[] { "CurrentId", "LastSearchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSearchHistories_TargetId",
                table: "AccountSearchHistories",
                column: "TargetId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountSearchHistories");
        }
    }
}
