using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CountPartialRefundsInRefundFigures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Already done by RecordPartialRefunds; repeated so the constraint can never meet an old full refund.
            migrationBuilder.Sql(RecordPartialRefunds.BackfillRefundedOrdersSql);

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_RefundedCents_MatchStatus",
                schema: "orders",
                table: "orders",
                sql: "(\"Status\" <> 'Refunded' OR \"RefundedCents\" = \"AmountCents\") AND (\"RefundedCents\" = 0 OR \"Status\" IN ('Paid', 'Refunded'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_RefundedCents_MatchStatus",
                schema: "orders",
                table: "orders");
        }
    }
}
