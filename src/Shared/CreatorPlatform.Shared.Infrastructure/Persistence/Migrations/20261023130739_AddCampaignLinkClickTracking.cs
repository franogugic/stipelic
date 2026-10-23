using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignLinkClickTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClickTracked",
                schema: "marketing",
                table: "campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UniqueClickCount",
                schema: "marketing",
                table: "campaigns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstClickedAt",
                schema: "marketing",
                table: "campaign_recipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "campaign_links",
                schema: "marketing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<short>(type: "smallint", nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UniqueClickCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalClickCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_links", x => x.Id);
                    table.CheckConstraint("CK_campaign_links_Label_Matches_Kind", "(\"Kind\" = 'Button' AND \"Label\" IS NOT NULL) OR (\"Kind\" = 'Text' AND \"Label\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_campaign_links_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "marketing",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_link_clicks",
                schema: "marketing",
                columns: table => new
                {
                    LinkId = table.Column<int>(type: "integer", nullable: false),
                    RecipientId = table.Column<int>(type: "integer", nullable: false),
                    FirstClickedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastClickedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClickCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_link_clicks", x => new { x.LinkId, x.RecipientId });
                    table.ForeignKey(
                        name: "FK_campaign_link_clicks_campaign_links_LinkId",
                        column: x => x.LinkId,
                        principalSchema: "marketing",
                        principalTable: "campaign_links",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_campaign_link_clicks_campaign_recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalSchema: "marketing",
                        principalTable: "campaign_recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_link_clicks_RecipientId",
                schema: "marketing",
                table: "campaign_link_clicks",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_links_CampaignId_Url",
                schema: "marketing",
                table: "campaign_links",
                columns: new[] { "CampaignId", "Url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_link_clicks",
                schema: "marketing");

            migrationBuilder.DropTable(
                name: "campaign_links",
                schema: "marketing");

            migrationBuilder.DropColumn(
                name: "ClickTracked",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "UniqueClickCount",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "FirstClickedAt",
                schema: "marketing",
                table: "campaign_recipients");
        }
    }
}
