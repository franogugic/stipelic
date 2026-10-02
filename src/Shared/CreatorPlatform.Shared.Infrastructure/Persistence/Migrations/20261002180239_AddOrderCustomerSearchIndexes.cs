using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <summary>Customer search on the orders list (case-insensitive substring of email or name). Trigram GIN
    /// indexes on the lower() expressions serve <c>lower("Email") LIKE '%term%'</c>, which a btree index cannot.
    /// Expression indexes can't be modelled in EF, hence raw SQL (the model snapshot does not track them).</summary>
    public partial class AddOrderCustomerSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pg_trgm;

                CREATE INDEX IF NOT EXISTS "IX_orders_Email_lower_trgm"
                    ON orders.orders USING gin (lower("Email") gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS "IX_orders_Name_lower_trgm"
                    ON orders.orders USING gin (lower("Name") gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The extension is left installed: other objects may come to depend on it.
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS orders."IX_orders_Name_lower_trgm";
                DROP INDEX IF EXISTS orders."IX_orders_Email_lower_trgm";
                """);
        }
    }
}
