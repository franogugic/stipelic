using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordPartialRefunds : Migration
    {
        /// <summary>Orders refunded before partial refunds existed were refunded in full. Idempotent.</summary>
        public const string BackfillRefundedOrdersSql = """
            UPDATE orders.orders SET "RefundedCents" = "AmountCents"
            WHERE "Status" = 'Refunded' AND "RefundedCents" <> "AmountCents";
            """;

        /// <summary>Their refund entries reversed the whole sale: they bring the order to its full amount. Idempotent.</summary>
        public const string BackfillRefundEntriesSql = """
            UPDATE payouts.ledger_entries e SET "RefundedToCents" = o."AmountCents"
            FROM orders.orders o
            WHERE o."Id" = e."OrderId" AND e."Type" IN ('RefundDebit', 'FeeRefundCredit') AND e."RefundedToCents" IS NULL;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_OrderId_Type",
                schema: "payouts",
                table: "ledger_entries");

            migrationBuilder.AddColumn<int>(
                name: "RefundedCents",
                schema: "orders",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RefundedToCents",
                schema: "payouts",
                table: "ledger_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(BackfillRefundedOrdersSql);
            migrationBuilder.Sql(BackfillRefundEntriesSql);

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_RefundedCents_WithinAmount",
                schema: "orders",
                table: "orders",
                sql: "\"RefundedCents\" >= 0 AND \"RefundedCents\" <= \"AmountCents\"");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_OrderId_Type_RefundedToCents",
                schema: "payouts",
                table: "ledger_entries",
                columns: new[] { "OrderId", "Type", "RefundedToCents" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ledger_entries_RefundedToCents_OnRefundsOnly",
                schema: "payouts",
                table: "ledger_entries",
                sql: "(\"Type\" IN ('RefundDebit', 'FeeRefundCredit')) = (\"RefundedToCents\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_RefundedCents_WithinAmount",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_OrderId_Type_RefundedToCents",
                schema: "payouts",
                table: "ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ledger_entries_RefundedToCents_OnRefundsOnly",
                schema: "payouts",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "RefundedCents",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "RefundedToCents",
                schema: "payouts",
                table: "ledger_entries");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_OrderId_Type",
                schema: "payouts",
                table: "ledger_entries",
                columns: new[] { "OrderId", "Type" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");
        }
    }
}
