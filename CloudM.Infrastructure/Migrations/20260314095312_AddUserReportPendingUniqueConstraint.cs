using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserReportPendingUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModerationReports_ReporterAccountId",
                table: "ModerationReports");

            migrationBuilder.Sql("""
                WITH ranked_reports AS (
                    SELECT
                        "ModerationReportId",
                        ROW_NUMBER() OVER (
                            PARTITION BY "ReporterAccountId", "TargetType", "TargetId"
                            ORDER BY
                                CASE WHEN "Status" = 1 THEN 0 ELSE 1 END,
                                "CreatedAt" DESC,
                                "ModerationReportId" DESC
                        ) AS row_number
                    FROM "ModerationReports"
                    WHERE "ReporterAccountId" IS NOT NULL
                      AND "SourceType" = 1
                      AND "Status" IN (0, 1)
                )
                UPDATE "ModerationReports" AS report
                SET
                    "Status" = 3,
                    "UpdatedAt" = COALESCE(report."UpdatedAt", NOW()),
                    "ResolvedAt" = COALESCE(report."ResolvedAt", NOW())
                FROM ranked_reports
                WHERE ranked_reports."ModerationReportId" = report."ModerationReportId"
                  AND ranked_reports.row_number > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_UserSubmittedPendingUnique",
                table: "ModerationReports",
                columns: new[] { "ReporterAccountId", "TargetType", "TargetId" },
                unique: true,
                filter: "\"ReporterAccountId\" IS NOT NULL AND \"SourceType\" = 1 AND \"Status\" IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModerationReports_UserSubmittedPendingUnique",
                table: "ModerationReports");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_ReporterAccountId",
                table: "ModerationReports",
                column: "ReporterAccountId");
        }
    }
}
