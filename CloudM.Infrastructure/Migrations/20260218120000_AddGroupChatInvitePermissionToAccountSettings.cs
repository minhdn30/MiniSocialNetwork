using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using CloudM.Infrastructure.Data;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260218120000_AddGroupChatInvitePermissionToAccountSettings")]
    public partial class AddGroupChatInvitePermissionToAccountSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupChatInvitePermission",
                table: "AccountSettings",
                type: "integer",
                nullable: false,
                defaultValue: 2);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupChatInvitePermission",
                table: "AccountSettings");
        }
    }
}
