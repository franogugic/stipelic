using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_products_PriceCents_NonNegative",
                schema: "products",
                table: "products",
                sql: "\"PriceCents\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_orders_Email",
                schema: "orders",
                table: "orders",
                column: "Email");

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_AmountCents_NonNegative",
                schema: "orders",
                table: "orders",
                sql: "\"AmountCents\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_creator_plans_PriceCents_NonNegative",
                schema: "creators",
                table: "creator_plans",
                sql: "\"PriceCents\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_products_PriceCents_NonNegative",
                schema: "products",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_orders_Email",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_AmountCents_NonNegative",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_creator_plans_PriceCents_NonNegative",
                schema: "creators",
                table: "creator_plans");
        }
    }
}
