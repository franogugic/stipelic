using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLandingPageIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_orders_CreatorId_LandingPageId_CreatedAt",
                schema: "orders",
                table: "orders",
                columns: new[] { "CreatorId", "LandingPageId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_CreatorId_LandingPageId_CreatedAt",
                schema: "orders",
                table: "orders");
        }
    }
}
