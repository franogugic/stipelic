using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CountPartialRefundsInRevenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KeptAmountCents",
                schema: "orders",
                table: "orders",
                type: "integer",
                nullable: false,
                computedColumnSql: "\"AmountCents\" - \"RefundedCents\"",
                stored: true);

            migrationBuilder.AddColumn<int>(
                name: "KeptPlatformFeeCents",
                schema: "orders",
                table: "orders",
                type: "integer",
                nullable: false,
                computedColumnSql: "\"PlatformFeeCents\" - CASE WHEN \"AmountCents\" > 0 THEN round(\"PlatformFeeCents\"::numeric * \"RefundedCents\" / \"AmountCents\")::integer ELSE 0 END",
                stored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeptAmountCents",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "KeptPlatformFeeCents",
                schema: "orders",
                table: "orders");
        }
    }
}
