using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountSettings",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailPrivacy = table.Column<int>(type: "integer", nullable: false),
                    PhonePrivacy = table.Column<int>(type: "integer", nullable: false),
                    AddressPrivacy = table.Column<int>(type: "integer", nullable: false),
                    DefaultPostPrivacy = table.Column<int>(type: "integer", nullable: false),
                    FollowerPrivacy = table.Column<int>(type: "integer", nullable: false),
                    FollowingPrivacy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountSettings", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_AccountSettings_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountSettings");
        }
    }
}
