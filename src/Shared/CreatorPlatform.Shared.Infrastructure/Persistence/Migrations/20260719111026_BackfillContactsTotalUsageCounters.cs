using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillContactsTotalUsageCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One-time backfill: seed the all-time "max_contacts" usage counter from existing capture
            // data, so enforcement (added alongside this migration) starts from an accurate baseline
            // instead of 0. PeriodStart/End are the fixed AllTime sentinels
            // (see UsagePeriodResolver.AllTimeStart/AllTimeEnd). ON CONFLICT DO NOTHING makes this safe
            // to re-run against an environment where some counters already exist.
            migrationBuilder.Sql("""
                INSERT INTO creators.creator_usage_counters
                    ("CreatorId", "UsageKey", "UsedValue", "PeriodStart", "PeriodEnd", "CreatedAt", "UpdatedAt")
                SELECT
                    lp."CreatorId",
                    'max_contacts',
                    count(DISTINCT ec."Email"),
                    '0001-01-01T00:00:00Z'::timestamptz,
                    '9999-12-31T00:00:00Z'::timestamptz,
                    now(),
                    now()
                FROM analytics.email_captures ec
                JOIN landing_pages.landing_pages lp ON lp."Id" = ec."LandingPageId"
                GROUP BY lp."CreatorId"
                ON CONFLICT ("CreatorId", "UsageKey", "PeriodStart", "PeriodEnd") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM creators.creator_usage_counters
                WHERE "UsageKey" = 'max_contacts'
                    AND "PeriodStart" = '0001-01-01T00:00:00Z'::timestamptz
                    AND "PeriodEnd" = '9999-12-31T00:00:00Z'::timestamptz;
                """);
        }
    }
}
