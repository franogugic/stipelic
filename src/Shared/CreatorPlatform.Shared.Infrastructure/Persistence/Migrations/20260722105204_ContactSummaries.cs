using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContactSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contact_summaries",
                schema: "marketing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    FirstCapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastCapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceLandingPageIds = table.Column<List<int>>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_summaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_summaries_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contact_summaries_CreatorId_Email",
                schema: "marketing",
                table: "contact_summaries",
                columns: new[] { "CreatorId", "Email" },
                unique: true);

            // One-time backfill from existing capture history — this table is maintained incrementally
            // from here on (see EmailCaptureService), this GROUP BY never runs again.
            migrationBuilder.Sql("""
                INSERT INTO marketing.contact_summaries
                    ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds")
                SELECT
                    lp."CreatorId",
                    ec."Email",
                    MIN(ec."CapturedAt"),
                    MAX(ec."CapturedAt"),
                    array_agg(DISTINCT ec."LandingPageId" ORDER BY ec."LandingPageId")
                FROM analytics.email_captures ec
                JOIN landing_pages.landing_pages lp ON lp."Id" = ec."LandingPageId"
                GROUP BY lp."CreatorId", ec."Email"
                ON CONFLICT ("CreatorId", "Email") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_summaries",
                schema: "marketing");
        }
    }
}
