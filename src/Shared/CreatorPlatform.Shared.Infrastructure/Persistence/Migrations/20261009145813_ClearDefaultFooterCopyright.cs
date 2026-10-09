using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <summary>Data only: footers still showing an untouched template default get an empty copyright, so the
    /// page shows "© {year} {brand}. All rights reserved." from the creator's brand. Two defaults existed: the
    /// original "© 2025 My Brand. All rights reserved." and, since AddSectionVariant, "© My Brand. All rights
    /// reserved.". Any other value is kept. Not reversible: an empty copyright could also be the creator's choice.</summary>
    public partial class ClearDefaultFooterCopyright : Migration
    {
        /// <summary>Public so an integration test can run it against seeded rows.</summary>
        public const string ClearSql = """
            UPDATE landing_pages.landing_page_sections
            SET "ContentJson" = jsonb_set("ContentJson", '{copyright}', '""'::jsonb)
            WHERE "Type" = 'Footer'
              AND "ContentJson" ->> 'copyright' IN ('© My Brand. All rights reserved.', '© 2025 My Brand. All rights reserved.');
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
