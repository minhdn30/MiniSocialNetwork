using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PostReactMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "Posts",
                newName: "AccountId");

            migrationBuilder.RenameColumn(
                name: "Url",
                table: "PostMedias",
                newName: "MediaUrl");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "EmailVerifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Comments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Accounts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateTable(
                name: "CommentReacts",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReactType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentReacts", x => new { x.CommentId, x.AccountId });
                    table.ForeignKey(
                        name: "FK_CommentReacts_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommentReacts_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "CommentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AccountId",
                table: "Posts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReacts_AccountId",
                table: "PostReacts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AccountId",
                table: "Comments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentReacts_AccountId",
                table: "CommentReacts",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Accounts_AccountId",
                table: "Comments",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId",
                principalTable: "Comments",
                principalColumn: "CommentId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PostReacts_Accounts_AccountId",
                table: "PostReacts",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Accounts_AccountId",
                table: "Posts",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Accounts_AccountId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_PostReacts_Accounts_AccountId",
                table: "PostReacts");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Accounts_AccountId",
                table: "Posts");

            migrationBuilder.DropTable(
                name: "CommentReacts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_AccountId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_PostReacts_AccountId",
                table: "PostReacts");

            migrationBuilder.DropIndex(
                name: "IX_Comments_AccountId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "EmailVerifications");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "Posts",
                newName: "OwnerId");

            migrationBuilder.RenameColumn(
                name: "MediaUrl",
                table: "PostMedias",
                newName: "Url");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Comments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Accounts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId",
                principalTable: "Comments",
                principalColumn: "CommentId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
