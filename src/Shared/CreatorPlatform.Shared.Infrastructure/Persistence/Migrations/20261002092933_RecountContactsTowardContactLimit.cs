using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <summary>The max_contacts usage counter used to grow on every capture, so a person signing up on three
    /// landing pages took three of the plan's contact slots. It now counts unique contacts (one per
    /// marketing.contact_summaries row); this recalculates every creator's all-time counter to match.</summary>
    public partial class RecountContactsTowardContactLimit : Migration
    {
        /// <summary>Idempotent: drops every all-time max_contacts row (including any written with a literal
        /// 0001-01-01 start, which the app never reads) and writes one row per creator that has contacts, with
        /// UsedValue = its contact count. A creator without contacts gets no row, which the app reads as 0.
        /// PeriodStart '-infinity' / PeriodEnd 9999-12-31 is exactly how Npgsql stores
        /// UsagePeriodResolver.AllTimeStart/AllTimeEnd (DateTimeOffset.MinValue is written as -infinity), so the
        /// app's own lookups find these rows. Public so the integration tests run the exact statement.</summary>
        public const string RecalculateContactsUsageSql = """
            DELETE FROM creators.creator_usage_counters
            WHERE "UsageKey" = 'max_contacts'
              AND "PeriodEnd" = TIMESTAMPTZ '9999-12-31 00:00:00+00';

            INSERT INTO creators.creator_usage_counters
                ("CreatorId", "UsageKey", "UsedValue", "PeriodStart", "PeriodEnd", "CreatedAt", "UpdatedAt")
            SELECT cs."CreatorId", 'max_contacts', COUNT(*)::int,
                   TIMESTAMPTZ '-infinity', TIMESTAMPTZ '9999-12-31 00:00:00+00', now(), now()
            FROM marketing.contact_summaries cs
            GROUP BY cs."CreatorId";
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RecalculateContactsUsageSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: the old per-capture totals were the bug being fixed and are not recoverable
            // from the data; rolling the code back keeps working with the recalculated (lower) counters.
        }
    }
}
