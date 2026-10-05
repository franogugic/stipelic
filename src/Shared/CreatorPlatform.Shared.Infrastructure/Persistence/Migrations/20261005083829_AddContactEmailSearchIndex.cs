using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <summary>Contacts search matches any part of the email (<c>"Email" LIKE '%term%'</c>), which the
    /// (CreatorId, Email) btree can't serve. A trigram GIN index on "Email" can; the emails are stored lower-case,
    /// so no lower() expression is needed. pg_trgm is enabled by AddOrderCustomerSearchIndexes (repeated here so the
    /// migration stands on its own). Trigram indexes can't be modelled in EF, hence raw SQL (the model snapshot does
    /// not track it).</summary>
    public partial class AddContactEmailSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pg_trgm;

                CREATE INDEX IF NOT EXISTS "IX_contact_summaries_Email_trgm"
                    ON marketing.contact_summaries USING gin ("Email" gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS marketing."IX_contact_summaries_Email_trgm";
                """);
        }
    }
}
