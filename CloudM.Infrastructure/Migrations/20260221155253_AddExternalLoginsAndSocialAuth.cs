using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalLoginsAndSocialAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Accounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "ExternalLogins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    ProviderUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalLogins_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogins_AccountId",
                table: "ExternalLogins",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogins_AccountId_Provider_Unique",
                table: "ExternalLogins",
                columns: new[] { "AccountId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalLogins_Provider_ProviderUserId_Unique",
                table: "ExternalLogins",
                columns: new[] { "Provider", "ProviderUserId" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION "fn_enforce_account_auth_method"()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    v_account_ids uuid[];
                    v_account_id uuid;
                BEGIN
                    v_account_ids := ARRAY[]::uuid[];

                    IF TG_OP = 'UPDATE' OR TG_OP = 'DELETE' THEN
                        v_account_ids := array_append(v_account_ids, OLD."AccountId");
                    END IF;

                    IF TG_OP = 'UPDATE' OR TG_OP = 'INSERT' THEN
                        v_account_ids := array_append(v_account_ids, NEW."AccountId");
                    END IF;

                    FOREACH v_account_id IN ARRAY v_account_ids LOOP
                        IF EXISTS (
                            SELECT 1
                            FROM "Accounts" a
                            WHERE a."AccountId" = v_account_id
                              AND a."PasswordHash" IS NULL
                              AND NOT EXISTS (
                                  SELECT 1
                                  FROM "ExternalLogins" el
                                  WHERE el."AccountId" = a."AccountId"
                              )
                        ) THEN
                            RAISE EXCEPTION
                                'Account % must have at least one authentication method.',
                                v_account_id
                                USING ERRCODE = '23514';
                        END IF;
                    END LOOP;

                    RETURN NULL;
                END;
                $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE CONSTRAINT TRIGGER "TR_Accounts_EnsureAuthMethod"
                AFTER INSERT OR UPDATE OF "PasswordHash" ON "Accounts"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW
                EXECUTE FUNCTION "fn_enforce_account_auth_method"();
                """);

            migrationBuilder.Sql(
                """
                CREATE CONSTRAINT TRIGGER "TR_ExternalLogins_EnsureAuthMethod"
                AFTER DELETE OR UPDATE OF "AccountId" ON "ExternalLogins"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW
                EXECUTE FUNCTION "fn_enforce_account_auth_method"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_ExternalLogins_EnsureAuthMethod" ON "ExternalLogins";""");
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "TR_Accounts_EnsureAuthMethod" ON "Accounts";""");
            migrationBuilder.Sql("""DROP FUNCTION IF EXISTS "fn_enforce_account_auth_method"();""");

            migrationBuilder.DropTable(
                name: "ExternalLogins");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Accounts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
