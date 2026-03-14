using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModerationReports",
                columns: table => new
                {
                    ModerationReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Detail = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    CreatedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationReports", x => x.ModerationReportId);
                    table.ForeignKey(
                        name: "FK_ModerationReports_Accounts_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModerationReports_Accounts_ResolvedByAdminId",
                        column: x => x.ResolvedByAdminId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ModerationReportActions",
                columns: table => new
                {
                    ModerationReportActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModerationReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationReportActions", x => x.ModerationReportActionId);
                    table.ForeignKey(
                        name: "FK_ModerationReportActions_Accounts_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModerationReportActions_ModerationReports_ModerationReportId",
                        column: x => x.ModerationReportId,
                        principalTable: "ModerationReports",
                        principalColumn: "ModerationReportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReportActions_AdminId_CreatedAt",
                table: "ModerationReportActions",
                columns: new[] { "AdminId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReportActions_ReportId_CreatedAt",
                table: "ModerationReportActions",
                columns: new[] { "ModerationReportId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_CreatedByAdminId",
                table: "ModerationReports",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_ResolvedByAdminId",
                table: "ModerationReports",
                column: "ResolvedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_Status_CreatedAt_ReportId",
                table: "ModerationReports",
                columns: new[] { "Status", "CreatedAt", "ModerationReportId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationReports_TargetType_TargetId_CreatedAt",
                table: "ModerationReports",
                columns: new[] { "TargetType", "TargetId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModerationReportActions");

            migrationBuilder.DropTable(
                name: "ModerationReports");
        }
    }
}
