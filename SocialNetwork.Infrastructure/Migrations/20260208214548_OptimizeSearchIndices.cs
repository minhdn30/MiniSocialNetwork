using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeSearchIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            // Create function to remove Vietnamese diacritics using translate()
            // This approach is simpler and more reliable than unaccent extension
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION immutable_unaccent(input_text text) 
                RETURNS text 
                LANGUAGE SQL IMMUTABLE PARALLEL SAFE STRICT
                AS $$
                    SELECT translate(
                        lower(input_text),
                        'áàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđ',
                        'aaaaaaaaaaaaaaaaaeeeeeeeeeeeiiiiiooooooooooooooooouuuuuuuuuuuyyyyyd'
                    );
                $$;
            ");

            // Create functional index for accent-insensitive search on FullName only
            // Username doesn't need unaccent because it's typically written without Vietnamese accents
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Accounts_Unaccent_FullName"" 
                ON ""Accounts"" USING GIN (immutable_unaccent(""FullName"") gin_trgm_ops);
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop functional index before removing function
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Accounts_Unaccent_FullName"";");
            
            // Drop wrapper function
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS immutable_unaccent(text);");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");





        }
    }
}
