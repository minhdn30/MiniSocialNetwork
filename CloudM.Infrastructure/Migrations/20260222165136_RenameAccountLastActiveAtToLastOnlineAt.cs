using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAccountLastActiveAtToLastOnlineAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastActiveAt",
                table: "Accounts",
                newName: "LastOnlineAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastOnlineAt",
                table: "Accounts",
                newName: "LastActiveAt");
        }
    }
}
