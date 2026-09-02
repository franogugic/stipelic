using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderStatusProductIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_orders_CreatorId_ProductId_CreatedAt",
                schema: "orders",
                table: "orders",
                columns: new[] { "CreatorId", "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_CreatorId_Status_CreatedAt",
                schema: "orders",
                table: "orders",
                columns: new[] { "CreatorId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_CreatorId_ProductId_CreatedAt",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_CreatorId_Status_CreatedAt",
                schema: "orders",
                table: "orders");
        }
    }
}
