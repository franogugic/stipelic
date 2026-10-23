using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOutboxFairQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatorId",
                schema: "email",
                table: "email_outbox_messages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "QueuePosition",
                schema: "email",
                table: "email_outbox_messages",
                type: "bigint",
                nullable: true);

            // Campaign mail still waiting at deploy time joins the fair queue: its creator comes from the campaign (the
            // correlation key starts with the campaign's public id), and each creator's messages are numbered from 0 in
            // their original order, so the creators waiting are interleaved from the first pass on.
            migrationBuilder.Sql("""
                UPDATE email.email_outbox_messages AS m
                SET "CreatorId" = c."CreatorId"
                FROM marketing.campaigns AS c
                WHERE m."Purpose" = 'CampaignBroadcast' AND m."Status" IN ('Pending', 'Processing')
                  AND c."PublicId"::text = split_part(m."CorrelationKey", ':', 1);

                UPDATE email.email_outbox_messages AS m
                SET "QueuePosition" = ranked.position
                FROM (SELECT "Id", row_number() OVER (PARTITION BY "CreatorId" ORDER BY "CreatedAt", "Id") - 1 AS position
                      FROM email.email_outbox_messages
                      WHERE "Purpose" = 'CampaignBroadcast' AND "Status" IN ('Pending', 'Processing')) AS ranked
                WHERE m."Id" = ranked."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_CampaignQueue",
                schema: "email",
                table: "email_outbox_messages",
                columns: new[] { "QueuePosition", "Id" },
                filter: "\"Purpose\" = 'CampaignBroadcast' AND \"Status\" IN ('Pending', 'Processing')");

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_CreatorCampaignQueue",
                schema: "email",
                table: "email_outbox_messages",
                columns: new[] { "CreatorId", "QueuePosition" },
                filter: "\"Purpose\" = 'CampaignBroadcast' AND \"Status\" IN ('Pending', 'Processing')");

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_TransactionalQueue",
                schema: "email",
                table: "email_outbox_messages",
                columns: new[] { "CreatedAt", "Id" },
                filter: "\"Purpose\" <> 'CampaignBroadcast' AND \"Status\" IN ('Pending', 'Processing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_outbox_messages_CampaignQueue",
                schema: "email",
                table: "email_outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_email_outbox_messages_CreatorCampaignQueue",
                schema: "email",
                table: "email_outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_email_outbox_messages_TransactionalQueue",
                schema: "email",
                table: "email_outbox_messages");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                schema: "email",
                table: "email_outbox_messages");

            migrationBuilder.DropColumn(
                name: "QueuePosition",
                schema: "email",
                table: "email_outbox_messages");
        }
    }
}
