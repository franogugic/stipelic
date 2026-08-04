using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorViewTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "creator_view_totals",
                schema: "analytics",
                columns: table => new
                {
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    TotalViews = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_view_totals", x => x.CreatorId);
                    table.ForeignKey(
                        name: "FK_creator_view_totals_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Backfill: this counter starts incrementing only from here forward (see
            // PageViewRepository.AddAsync), so seed it from every page_views row that already exists —
            // otherwise creators with view history would show 0 until their next fresh view.
            migrationBuilder.Sql("""
                INSERT INTO analytics.creator_view_totals ("CreatorId", "TotalViews")
                SELECT lp."CreatorId", COUNT(*)
                FROM analytics.page_views pv
                JOIN landing_pages.landing_pages lp ON lp."Id" = pv."LandingPageId"
                GROUP BY lp."CreatorId"
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creator_view_totals",
                schema: "analytics");
        }
    }
}
