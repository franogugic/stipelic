using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "marketing");

            migrationBuilder.CreateTable(
                name: "campaigns",
                schema: "marketing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    CtaLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CtaUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AudienceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LandingPageId = table.Column<int>(type: "integer", nullable: true),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.Id);
                    table.CheckConstraint("CK_campaigns_Audience_Matches_Fk", "(\"AudienceType\" = 'LandingPage' AND \"LandingPageId\" IS NOT NULL AND \"ProductId\" IS NULL) OR (\"AudienceType\" = 'Product' AND \"ProductId\" IS NOT NULL AND \"LandingPageId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_campaigns_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_campaigns_landing_pages_LandingPageId",
                        column: x => x.LandingPageId,
                        principalSchema: "landing_pages",
                        principalTable: "landing_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_campaigns_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "products",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unsubscribes",
                schema: "marketing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UnsubscribedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unsubscribes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_unsubscribes_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "campaign_recipients",
                schema: "marketing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_recipients_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "marketing",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_recipients_CampaignId_Email",
                schema: "marketing",
                table: "campaign_recipients",
                columns: new[] { "CampaignId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_CreatorId_CreatedAt",
                schema: "marketing",
                table: "campaigns",
                columns: new[] { "CreatorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_LandingPageId",
                schema: "marketing",
                table: "campaigns",
                column: "LandingPageId");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_ProductId",
                schema: "marketing",
                table: "campaigns",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_PublicId",
                schema: "marketing",
                table: "campaigns",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unsubscribes_CreatorId_Email",
                schema: "marketing",
                table: "unsubscribes",
                columns: new[] { "CreatorId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_recipients",
                schema: "marketing");

            migrationBuilder.DropTable(
                name: "unsubscribes",
                schema: "marketing");

            migrationBuilder.DropTable(
                name: "campaigns",
                schema: "marketing");
        }
    }
}
