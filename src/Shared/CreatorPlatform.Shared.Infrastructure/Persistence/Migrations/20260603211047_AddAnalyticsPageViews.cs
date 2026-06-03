using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsPageViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "analytics");

            migrationBuilder.CreateTable(
                name: "page_views",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandingPageId = table.Column<int>(type: "integer", nullable: false),
                    VisitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_page_views", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_page_views_LandingPageId_ViewedAt",
                schema: "analytics",
                table: "page_views",
                columns: new[] { "LandingPageId", "ViewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_page_views_LandingPageId_VisitorId_ViewedAt",
                schema: "analytics",
                table: "page_views",
                columns: new[] { "LandingPageId", "VisitorId", "ViewedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "page_views",
                schema: "analytics");
        }
    }
}
