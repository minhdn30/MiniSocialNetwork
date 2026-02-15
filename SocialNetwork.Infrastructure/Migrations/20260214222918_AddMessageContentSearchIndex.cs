using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageContentSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GIN trigram index on immutable_unaccent(Content) for fast fuzzy search
            // Same pattern as IX_Accounts_Unaccent_FullName in OptimizeSearchIndices migration
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Messages_Unaccent_Content""
                ON ""Messages"" USING GIN (immutable_unaccent(""Content"") gin_trgm_ops);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Messages_Unaccent_Content"";");
        }
    }
}
