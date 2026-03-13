using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountSettingsSoundEffects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SoundEffectsEnabled",
                table: "AccountSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoundEffectsEnabled",
                table: "AccountSettings");
        }
    }
}
