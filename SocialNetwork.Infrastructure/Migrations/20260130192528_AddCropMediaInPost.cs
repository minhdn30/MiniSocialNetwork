using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCropMediaInPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FeedAspectRatio",
                table: "Posts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "CropHeight",
                table: "PostMedias",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "CropWidth",
                table: "PostMedias",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "CropX",
                table: "PostMedias",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "CropY",
                table: "PostMedias",
                type: "real",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedAspectRatio",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "CropHeight",
                table: "PostMedias");

            migrationBuilder.DropColumn(
                name: "CropWidth",
                table: "PostMedias");

            migrationBuilder.DropColumn(
                name: "CropX",
                table: "PostMedias");

            migrationBuilder.DropColumn(
                name: "CropY",
                table: "PostMedias");
        }
    }
}
