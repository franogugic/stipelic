using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <summary>F5.0: max_contacts counts contacts that are not unsubscribed — the counter now equals a creator's
    /// marketing.contact_summaries rows whose email has no marketing.unsubscribes row. This recounts every creator's
    /// all-time counter to that invariant; from here on unsubscribes, deletes and sign-ups keep it.</summary>
    public partial class RecountContactLimitWithoutUnsubscribed : Migration
    {
        /// <summary>Idempotent, same shape as <see cref="RecountContactsTowardContactLimit.RecalculateContactsUsageSql"/>:
        /// drops every all-time max_contacts row and writes one per creator with at least one counted contact (a
        /// creator without one gets no row, which the app reads as 0). Public so the integration tests run the exact
        /// statement.</summary>
        public const string RecalculateContactsUsageSql = """
            DELETE FROM creators.creator_usage_counters
            WHERE "UsageKey" = 'max_contacts'
              AND "PeriodEnd" = TIMESTAMPTZ '9999-12-31 00:00:00+00';

            INSERT INTO creators.creator_usage_counters
                ("CreatorId", "UsageKey", "UsedValue", "PeriodStart", "PeriodEnd", "CreatedAt", "UpdatedAt")
            SELECT cs."CreatorId", 'max_contacts', COUNT(*)::int,
                   TIMESTAMPTZ '-infinity', TIMESTAMPTZ '9999-12-31 00:00:00+00', now(), now()
            FROM marketing.contact_summaries cs
            WHERE NOT EXISTS (
                SELECT 1 FROM marketing.unsubscribes u
                WHERE u."CreatorId" = cs."CreatorId" AND u."Email" = cs."Email"
            )
            GROUP BY cs."CreatorId";
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RecalculateContactsUsageSql);
        }

        /// <summary>Back to counting every contact, what the code before F5.0 expects.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RecountContactsTowardContactLimit.RecalculateContactsUsageSql);
        }
    }
}
