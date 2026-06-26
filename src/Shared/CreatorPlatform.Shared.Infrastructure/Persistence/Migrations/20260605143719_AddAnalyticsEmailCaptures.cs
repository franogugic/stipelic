using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsEmailCaptures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_captures",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandingPageId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_captures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_captures_LandingPageId_CapturedAt",
                schema: "analytics",
                table: "email_captures",
                columns: new[] { "LandingPageId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_email_captures_LandingPageId_Email",
                schema: "analytics",
                table: "email_captures",
                columns: new[] { "LandingPageId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_captures",
                schema: "analytics");
        }
    }
}
