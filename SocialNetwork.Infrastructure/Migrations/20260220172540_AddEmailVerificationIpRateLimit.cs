using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationIpRateLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailVerificationIpRateLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IpAddress = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SendCountInWindow = table.Column<int>(type: "integer", nullable: false),
                    SendWindowStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DailySendCount = table.Column<int>(type: "integer", nullable: false),
                    DailyWindowStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationIpRateLimits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationIpRateLimits_IpAddress_Unique",
                table: "EmailVerificationIpRateLimits",
                column: "IpAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationIpRateLimits_LockedUntil",
                table: "EmailVerificationIpRateLimits",
                column: "LockedUntil");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationIpRateLimits_UpdatedAt",
                table: "EmailVerificationIpRateLimits",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailVerificationIpRateLimits");
        }
    }
}
