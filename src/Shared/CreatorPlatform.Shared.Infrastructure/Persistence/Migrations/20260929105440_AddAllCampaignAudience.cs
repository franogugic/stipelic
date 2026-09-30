using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAllCampaignAudience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_campaigns_Audience_Matches_Fk",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.AddCheckConstraint(
                name: "CK_campaigns_Audience_Matches_Fk",
                schema: "marketing",
                table: "campaigns",
                sql: "(\"AudienceType\" = 'LandingPage' AND \"LandingPageId\" IS NOT NULL AND \"ProductId\" IS NULL) OR (\"AudienceType\" = 'Product' AND \"ProductId\" IS NOT NULL AND \"LandingPageId\" IS NULL) OR (\"AudienceType\" = 'All' AND \"LandingPageId\" IS NULL AND \"ProductId\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_campaigns_Audience_Matches_Fk",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.AddCheckConstraint(
                name: "CK_campaigns_Audience_Matches_Fk",
                schema: "marketing",
                table: "campaigns",
                sql: "(\"AudienceType\" = 'LandingPage' AND \"LandingPageId\" IS NOT NULL AND \"ProductId\" IS NULL) OR (\"AudienceType\" = 'Product' AND \"ProductId\" IS NOT NULL AND \"LandingPageId\" IS NULL)");
        }
    }
}
