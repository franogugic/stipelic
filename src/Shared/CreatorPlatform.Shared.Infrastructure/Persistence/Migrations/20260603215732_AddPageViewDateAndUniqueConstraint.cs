using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPageViewDateAndUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_page_views_LandingPageId_VisitorId_ViewedAt",
                schema: "analytics",
                table: "page_views");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ViewedDate",
                schema: "analytics",
                table: "page_views",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_page_views_LandingPageId_VisitorId_ViewedDate",
                schema: "analytics",
                table: "page_views",
                columns: new[] { "LandingPageId", "VisitorId", "ViewedDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_page_views_LandingPageId_VisitorId_ViewedDate",
                schema: "analytics",
                table: "page_views");

            migrationBuilder.DropColumn(
                name: "ViewedDate",
                schema: "analytics",
                table: "page_views");

            migrationBuilder.CreateIndex(
                name: "IX_page_views_LandingPageId_VisitorId_ViewedAt",
                schema: "analytics",
                table: "page_views",
                columns: new[] { "LandingPageId", "VisitorId", "ViewedAt" });
        }
    }
}
