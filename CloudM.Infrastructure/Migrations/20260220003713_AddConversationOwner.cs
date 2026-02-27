using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Owner",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            // Backfill owner for existing data:
            // - Private conversations: Owner = NULL
            // - Group conversations:
            //   1) creator if still active member
            //   2) first active admin
            //   3) first active member
            //   4) fallback creator (for legacy groups with no active members)
            migrationBuilder.Sql("""
                UPDATE "Conversations"
                SET "Owner" = NULL
                WHERE "IsGroup" = FALSE;
                """);

            migrationBuilder.Sql("""
                WITH candidate_owner AS (
                    SELECT
                        c."ConversationId",
                        COALESCE(
                            CASE
                                WHEN EXISTS (
                                    SELECT 1
                                    FROM "ConversationMembers" cm
                                    WHERE cm."ConversationId" = c."ConversationId"
                                      AND cm."AccountId" = c."CreatedBy"
                                      AND cm."HasLeft" = FALSE
                                ) THEN c."CreatedBy"
                                ELSE NULL
                            END,
                            (
                                SELECT cm1."AccountId"
                                FROM "ConversationMembers" cm1
                                WHERE cm1."ConversationId" = c."ConversationId"
                                  AND cm1."HasLeft" = FALSE
                                  AND cm1."IsAdmin" = TRUE
                                ORDER BY cm1."JoinedAt", cm1."AccountId"
                                LIMIT 1
                            ),
                            (
                                SELECT cm2."AccountId"
                                FROM "ConversationMembers" cm2
                                WHERE cm2."ConversationId" = c."ConversationId"
                                  AND cm2."HasLeft" = FALSE
                                ORDER BY cm2."JoinedAt", cm2."AccountId"
                                LIMIT 1
                            ),
                            c."CreatedBy"
                        ) AS "OwnerId"
                    FROM "Conversations" c
                    WHERE c."IsGroup" = TRUE
                )
                UPDATE "Conversations" c
                SET "Owner" = co."OwnerId"
                FROM candidate_owner co
                WHERE c."ConversationId" = co."ConversationId";
                """);

            // Ensure current owner always has admin flag when owner is an active member.
            migrationBuilder.Sql("""
                UPDATE "ConversationMembers" cm
                SET "IsAdmin" = TRUE
                FROM "Conversations" c
                WHERE c."ConversationId" = cm."ConversationId"
                  AND c."IsGroup" = TRUE
                  AND c."Owner" IS NOT NULL
                  AND cm."AccountId" = c."Owner"
                  AND cm."HasLeft" = FALSE;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Owner",
                table: "Conversations",
                column: "Owner");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_GroupOwner",
                table: "Conversations",
                sql: "(\"IsGroup\" = FALSE AND \"Owner\" IS NULL) OR (\"IsGroup\" = TRUE AND \"Owner\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Accounts_Owner",
                table: "Conversations",
                column: "Owner",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Accounts_Owner",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_Owner",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_GroupOwner",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Owner",
                table: "Conversations");
        }
    }
}
