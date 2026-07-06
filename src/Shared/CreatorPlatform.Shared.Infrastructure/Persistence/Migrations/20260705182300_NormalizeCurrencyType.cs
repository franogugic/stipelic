using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeCurrencyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize legacy uppercase/mixed currency values to enum string names.
            // EF Core HasConversion<string>() stores enum member name (e.g. "Eur", "Usd").
            migrationBuilder.Sql("""
                UPDATE orders.orders
                SET "Currency" = 'Eur'
                WHERE UPPER("Currency") = 'EUR' AND "Currency" != 'Eur';
                """);

            migrationBuilder.Sql("""
                UPDATE orders.orders
                SET "Currency" = 'Usd'
                WHERE UPPER("Currency") = 'USD' AND "Currency" != 'Usd';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to uppercase format (safe no-data-loss rollback)
            migrationBuilder.Sql("""
                UPDATE orders.orders
                SET "Currency" = 'EUR'
                WHERE "Currency" = 'Eur';
                """);

            migrationBuilder.Sql("""
                UPDATE orders.orders
                SET "Currency" = 'USD'
                WHERE "Currency" = 'Usd';
                """);
        }
    }
}
