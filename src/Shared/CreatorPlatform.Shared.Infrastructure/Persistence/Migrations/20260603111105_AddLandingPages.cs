using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLandingPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "landing_pages");

            migrationBuilder.CreateTable(
                name: "landing_pages",
                schema: "landing_pages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CustomDomain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_landing_pages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "landing_page_sections",
                schema: "landing_pages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    LandingPageId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    BackgroundColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_landing_page_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_landing_page_sections_landing_pages_LandingPageId",
                        column: x => x.LandingPageId,
                        principalSchema: "landing_pages",
                        principalTable: "landing_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_landing_page_sections_LandingPageId_SortOrder",
                schema: "landing_pages",
                table: "landing_page_sections",
                columns: new[] { "LandingPageId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_landing_page_sections_PublicId",
                schema: "landing_pages",
                table: "landing_page_sections",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_landing_pages_CreatorId_Slug",
                schema: "landing_pages",
                table: "landing_pages",
                columns: new[] { "CreatorId", "Slug" },
                unique: true,
                filter: "\"Status\" != 'Archived'");

            migrationBuilder.CreateIndex(
                name: "IX_landing_pages_CreatorId_Status",
                schema: "landing_pages",
                table: "landing_pages",
                columns: new[] { "CreatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_landing_pages_CustomDomain",
                schema: "landing_pages",
                table: "landing_pages",
                column: "CustomDomain",
                unique: true,
                filter: "\"CustomDomain\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_landing_pages_PublicId",
                schema: "landing_pages",
                table: "landing_pages",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "landing_page_sections",
                schema: "landing_pages");

            migrationBuilder.DropTable(
                name: "landing_pages",
                schema: "landing_pages");
        }
    }
}
