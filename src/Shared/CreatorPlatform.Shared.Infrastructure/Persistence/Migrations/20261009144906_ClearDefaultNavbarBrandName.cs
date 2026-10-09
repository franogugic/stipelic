using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <summary>Data only: navbars still showing the untouched template default "My Brand" get an empty
    /// brandName, so the page shows the creator's brand (brand name, else the workspace name). Any other value —
    /// including a creator who typed "my brand" in another case or with spaces — is kept. Not reversible: an empty
    /// brandName could also be the creator's own choice.</summary>
    public partial class ClearDefaultNavbarBrandName : Migration
    {
        /// <summary>Public so an integration test can run it against seeded rows.</summary>
        public const string ClearSql = """
            UPDATE landing_pages.landing_page_sections
            SET "ContentJson" = jsonb_set("ContentJson", '{brandName}', '""'::jsonb)
            WHERE "Type" = 'Navbar' AND "ContentJson" ->> 'brandName' = 'My Brand';
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ClearSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: see the summary.
        }
    }
}
