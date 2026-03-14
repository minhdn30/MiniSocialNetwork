using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationReportReporterAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReporterAccountId",
                table: "ModerationReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_ReporterAccountId",
                table: "ModerationReports",
                column: "ReporterAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationReports_Accounts_ReporterAccountId",
                table: "ModerationReports",
                column: "ReporterAccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("""
                UPDATE "ModerationReports"
                SET "ReporterAccountId" = "CreatedByAdminId",
                    "CreatedByAdminId" = NULL
                WHERE "SourceType" = 1
                  AND "ReporterAccountId" IS NULL
                  AND "CreatedByAdminId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModerationReports_Accounts_ReporterAccountId",
                table: "ModerationReports");

            migrationBuilder.DropIndex(
                name: "IX_ModerationReports_ReporterAccountId",
                table: "ModerationReports");

            migrationBuilder.Sql("""
                UPDATE "ModerationReports"
                SET "CreatedByAdminId" = "ReporterAccountId"
                WHERE "SourceType" = 1
                  AND "CreatedByAdminId" IS NULL
                  AND "ReporterAccountId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "ReporterAccountId",
                table: "ModerationReports");
        }
    }
}
