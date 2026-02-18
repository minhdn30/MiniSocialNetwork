using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SocialNetwork.Infrastructure.Data;

#nullable disable

namespace SocialNetwork.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260218143000_AddUsernameTrigramSearchIndex")]
    public partial class AddUsernameTrigramSearchIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Accounts_Username_Trgm""
                ON ""Accounts"" USING GIN (""Username"" gin_trgm_ops);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Accounts_Username_Trgm"";");
        }
    }
}
