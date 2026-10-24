using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkCreatorEmailsToTheirCreator : Migration
    {
        /// <summary>Gives the creator to every creator email queued before it was recorded, through its correlation key:
        /// a buyer's order email (the order's PublicId), a lead's download email ("lead:{captureId}:{productId}") and a
        /// campaign message ("{campaignPublicId}:{recipientId}"; the ones still waiting got it with the fair queue).
        /// Account mail keeps no creator. Only rows without one are touched, so it can run twice.</summary>
        public const string BackfillCreatorIdSql = """
            UPDATE email.email_outbox_messages AS m
            SET "CreatorId" = o."CreatorId"
            FROM orders.orders AS o
            WHERE m."Purpose" = 'OrderAccess' AND m."CreatorId" IS NULL
              AND o."PublicId"::text = m."CorrelationKey";

            UPDATE email.email_outbox_messages AS m
            SET "CreatorId" = p."CreatorId"
            FROM products.products AS p
            WHERE m."Purpose" = 'LeadMagnetDelivery' AND m."CreatorId" IS NULL
              AND split_part(m."CorrelationKey", ':', 3) ~ '^[0-9]+$'
              AND p."Id" = split_part(m."CorrelationKey", ':', 3)::integer;

            UPDATE email.email_outbox_messages AS m
            SET "CreatorId" = c."CreatorId"
            FROM marketing.campaigns AS c
            WHERE m."Purpose" = 'CampaignBroadcast' AND m."CreatorId" IS NULL
              AND c."PublicId"::text = split_part(m."CorrelationKey", ':', 1);
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(BackfillCreatorIdSql);

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_CreatorId",
                schema: "email",
                table: "email_outbox_messages",
                column: "CreatorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_outbox_messages_CreatorId",
                schema: "email",
                table: "email_outbox_messages");
        }
    }
}
