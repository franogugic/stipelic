using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaveOrdersBeforeTheirCheckoutSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StripeCheckoutSessionId",
                schema: "orders",
                table: "orders",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_Session_Unless_PendingOrFailed",
                schema: "orders",
                table: "orders",
                sql: "\"StripeCheckoutSessionId\" IS NOT NULL OR \"Status\" IN ('Pending', 'Failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_Session_Unless_PendingOrFailed",
                schema: "orders",
                table: "orders");

            // NOT NULL + unique again: orders that never got a session get a unique placeholder instead of ''.
            migrationBuilder.Sql("""
                UPDATE orders.orders SET "StripeCheckoutSessionId" = 'none_' || "PublicId"::text
                WHERE "StripeCheckoutSessionId" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "StripeCheckoutSessionId",
                schema: "orders",
                table: "orders",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
