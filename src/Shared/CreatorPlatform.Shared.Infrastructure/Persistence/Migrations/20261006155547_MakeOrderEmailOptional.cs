using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <summary>The public page's buy button goes straight to Stripe Checkout, which collects the buyer's email; a
    /// Pending order has none until checkout.session.completed fills it. Paid orders always have one.</summary>
    public partial class MakeOrderEmailOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "orders",
                table: "orders",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only abandoned Pending checkouts can be NULL; they never get an email.
            migrationBuilder.Sql("""
                UPDATE orders.orders SET "Email" = '' WHERE "Email" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "orders",
                table: "orders",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);
        }
    }
}
