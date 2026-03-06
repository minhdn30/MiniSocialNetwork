using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenEmailVerificationSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EmailVerification is a temporary challenge table.
            // Purge legacy rows before switching schema + unique email constraint.
            migrationBuilder.Sql("""DELETE FROM "EmailVerifications";""");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "EmailVerifications");

            migrationBuilder.AddColumn<string>(
                name: "CodeHash",
                table: "EmailVerifications",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CodeSalt",
                table: "EmailVerifications",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsumedAt",
                table: "EmailVerifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailySendCount",
                table: "EmailVerifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DailyWindowStartedAt",
                table: "EmailVerifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FailedAttempts",
                table: "EmailVerifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSentAt",
                table: "EmailVerifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntil",
                table: "EmailVerifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SendCountInWindow",
                table: "EmailVerifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SendWindowStartedAt",
                table: "EmailVerifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerifications_Email_Unique",
                table: "EmailVerifications",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerifications_ExpiredAt",
                table: "EmailVerifications",
                column: "ExpiredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailVerifications_Email_Unique",
                table: "EmailVerifications");

            migrationBuilder.DropIndex(
                name: "IX_EmailVerifications_ExpiredAt",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "CodeHash",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "CodeSalt",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "ConsumedAt",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "DailySendCount",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "DailyWindowStartedAt",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "FailedAttempts",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "LastSentAt",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "SendCountInWindow",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "SendWindowStartedAt",
                table: "EmailVerifications");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "EmailVerifications",
                type: "varchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");
        }
    }
}
