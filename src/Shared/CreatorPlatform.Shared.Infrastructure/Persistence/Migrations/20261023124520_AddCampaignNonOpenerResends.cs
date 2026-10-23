using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignNonOpenerResends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_campaigns_Audience_Matches_Fk",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.AddColumn<int>(
                name: "ResendRound",
                schema: "marketing",
                table: "campaigns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceCampaignId",
                schema: "marketing",
                table: "campaigns",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_SourceCampaignId",
                schema: "marketing",
                table: "campaigns",
                column: "SourceCampaignId",
                filter: "\"SourceCampaignId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_campaigns_Audience_Matches_Fk",
                schema: "marketing",
                table: "campaigns",
                sql: "(\"AudienceType\" = 'LandingPage' AND \"LandingPageId\" IS NOT NULL AND \"ProductId\" IS NULL AND \"SourceCampaignId\" IS NULL) OR (\"AudienceType\" = 'Product' AND \"ProductId\" IS NOT NULL AND \"LandingPageId\" IS NULL AND \"SourceCampaignId\" IS NULL) OR (\"AudienceType\" = 'All' AND \"LandingPageId\" IS NULL AND \"ProductId\" IS NULL AND \"SourceCampaignId\" IS NULL) OR (\"AudienceType\" = 'CampaignNonOpeners' AND \"SourceCampaignId\" IS NOT NULL AND \"LandingPageId\" IS NULL AND \"ProductId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_campaigns_ResendRound",
                schema: "marketing",
                table: "campaigns",
                sql: "(\"SourceCampaignId\" IS NULL AND \"ResendRound\" IS NULL) OR (\"SourceCampaignId\" IS NOT NULL AND \"ResendRound\" BETWEEN 1 AND 5)");

            migrationBuilder.AddForeignKey(
                name: "FK_campaigns_campaigns_SourceCampaignId",
                schema: "marketing",
                table: "campaigns",
                column: "SourceCampaignId",
                principalSchema: "marketing",
                principalTable: "campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_campaigns_campaigns_SourceCampaignId",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropIndex(
                name: "IX_campaigns_SourceCampaignId",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_campaigns_Audience_Matches_Fk",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_campaigns_ResendRound",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "ResendRound",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "SourceCampaignId",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.AddCheckConstraint(
                name: "CK_campaigns_Audience_Matches_Fk",
                schema: "marketing",
                table: "campaigns",
                sql: "(\"AudienceType\" = 'LandingPage' AND \"LandingPageId\" IS NOT NULL AND \"ProductId\" IS NULL) OR (\"AudienceType\" = 'Product' AND \"ProductId\" IS NOT NULL AND \"LandingPageId\" IS NULL) OR (\"AudienceType\" = 'All' AND \"LandingPageId\" IS NULL AND \"ProductId\" IS NULL)");
        }
    }
}
