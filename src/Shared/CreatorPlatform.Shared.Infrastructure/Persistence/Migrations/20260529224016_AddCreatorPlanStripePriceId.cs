using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CreatorPlatformDbContext))]
    [Migration("20260529224016_AddCreatorPlanStripePriceId")]
    public partial class AddCreatorPlanStripePriceId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripePriceId",
                schema: "creators",
                table: "creator_plans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripePriceId",
                schema: "creators",
                table: "creator_plans");
        }
    }
}
