using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_orders_LandingPageId",
                schema: "orders",
                table: "orders",
                column: "LandingPageId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ProductId",
                schema: "orders",
                table: "orders",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_landing_pages_ProductId",
                schema: "landing_pages",
                table: "landing_pages",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_email_captures_ProductId",
                schema: "analytics",
                table: "email_captures",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_email_captures_landing_pages_LandingPageId",
                schema: "analytics",
                table: "email_captures",
                column: "LandingPageId",
                principalSchema: "landing_pages",
                principalTable: "landing_pages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_email_captures_products_ProductId",
                schema: "analytics",
                table: "email_captures",
                column: "ProductId",
                principalSchema: "products",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_landing_pages_creators_CreatorId",
                schema: "landing_pages",
                table: "landing_pages",
                column: "CreatorId",
                principalSchema: "creators",
                principalTable: "creators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_landing_pages_products_ProductId",
                schema: "landing_pages",
                table: "landing_pages",
                column: "ProductId",
                principalSchema: "products",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_creators_CreatorId",
                schema: "orders",
                table: "orders",
                column: "CreatorId",
                principalSchema: "creators",
                principalTable: "creators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_landing_pages_LandingPageId",
                schema: "orders",
                table: "orders",
                column: "LandingPageId",
                principalSchema: "landing_pages",
                principalTable: "landing_pages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_products_ProductId",
                schema: "orders",
                table: "orders",
                column: "ProductId",
                principalSchema: "products",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_page_views_landing_pages_LandingPageId",
                schema: "analytics",
                table: "page_views",
                column: "LandingPageId",
                principalSchema: "landing_pages",
                principalTable: "landing_pages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_creators_CreatorId",
                schema: "products",
                table: "products",
                column: "CreatorId",
                principalSchema: "creators",
                principalTable: "creators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_email_captures_landing_pages_LandingPageId",
                schema: "analytics",
                table: "email_captures");

            migrationBuilder.DropForeignKey(
                name: "FK_email_captures_products_ProductId",
                schema: "analytics",
                table: "email_captures");

            migrationBuilder.DropForeignKey(
                name: "FK_landing_pages_creators_CreatorId",
                schema: "landing_pages",
                table: "landing_pages");

            migrationBuilder.DropForeignKey(
                name: "FK_landing_pages_products_ProductId",
                schema: "landing_pages",
                table: "landing_pages");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_creators_CreatorId",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_landing_pages_LandingPageId",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_products_ProductId",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_page_views_landing_pages_LandingPageId",
                schema: "analytics",
                table: "page_views");

            migrationBuilder.DropForeignKey(
                name: "FK_products_creators_CreatorId",
                schema: "products",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_orders_LandingPageId",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_ProductId",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_landing_pages_ProductId",
                schema: "landing_pages",
                table: "landing_pages");

            migrationBuilder.DropIndex(
                name: "IX_email_captures_ProductId",
                schema: "analytics",
                table: "email_captures");
        }
    }
}
