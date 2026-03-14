using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    AdminAuditLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ActionType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    TargetId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    RequestIp = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.AdminAuditLogId);
                    table.ForeignKey(
                        name: "FK_AdminAuditLogs_Accounts_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_AdminId_CreatedAt",
                table: "AdminAuditLogs",
                columns: new[] { "AdminId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CreatedAt_AdminAuditLogId",
                table: "AdminAuditLogs",
                columns: new[] { "CreatedAt", "AdminAuditLogId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_Module_ActionType_CreatedAt",
                table: "AdminAuditLogs",
                columns: new[] { "Module", "ActionType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");
        }
    }
}
