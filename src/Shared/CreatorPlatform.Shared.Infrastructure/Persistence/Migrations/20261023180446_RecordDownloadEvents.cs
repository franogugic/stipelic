using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <summary>R6.2: one row per download through a personal link, written with the link's counter bump. No backfill:
    /// downloads before this release are only in access_grants.DownloadCount.</summary>
    public partial class RecordDownloadEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "download_events",
                schema: "products",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrantId = table.Column<int>(type: "integer", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    LandingPageId = table.Column<int>(type: "integer", nullable: true),
                    DownloadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_download_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_download_events_access_grants_GrantId",
                        column: x => x.GrantId,
                        principalSchema: "products",
                        principalTable: "access_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_download_events_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_download_events_landing_pages_LandingPageId",
                        column: x => x.LandingPageId,
                        principalSchema: "landing_pages",
                        principalTable: "landing_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_download_events_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "products",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_download_events_CreatorId_DownloadedAt",
                schema: "products",
                table: "download_events",
                columns: new[] { "CreatorId", "DownloadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_download_events_GrantId",
                schema: "products",
                table: "download_events",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_download_events_LandingPageId_DownloadedAt",
                schema: "products",
                table: "download_events",
                columns: new[] { "LandingPageId", "DownloadedAt" },
                filter: "\"LandingPageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_download_events_ProductId_DownloadedAt",
                schema: "products",
                table: "download_events",
                columns: new[] { "ProductId", "DownloadedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "download_events",
                schema: "products");
        }
    }
}
