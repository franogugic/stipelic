using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrackWebhookFailureAttempts : Migration
    {
        /// <summary>Existing rows: one attempt, failed when they occurred. The controller stored the raw event (JSON) of
        /// a handler that threw — replayable; the creator webhook service stored a description of a manual fix. Idempotent.</summary>
        public const string BackfillSql = """
            UPDATE payments.webhook_failures
            SET "Attempts" = GREATEST("Attempts", 1),
                "LastFailedAt" = CASE WHEN "LastFailedAt" < "OccurredAt" THEN "OccurredAt" ELSE "LastFailedAt" END,
                "Kind" = CASE WHEN "Kind" <> '' THEN "Kind"
                              WHEN left(btrim("Payload"), 1) = chr(123) THEN 'HandlerFailed' -- an opening brace: a JSON event
                              ELSE 'NeedsAttention' END;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_webhook_failures_IsResolved_OccurredAt",
                schema: "payments",
                table: "webhook_failures");

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                schema: "payments",
                table: "webhook_failures",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "payments",
                table: "webhook_failures",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastFailedAt",
                schema: "payments",
                table: "webhook_failures",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql(BackfillSql);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_failures_IsResolved_LastFailedAt",
                schema: "payments",
                table: "webhook_failures",
                columns: new[] { "IsResolved", "LastFailedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_webhook_failures_Attempts_Positive",
                schema: "payments",
                table: "webhook_failures",
                sql: "\"Attempts\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_webhook_failures_IsResolved_LastFailedAt",
                schema: "payments",
                table: "webhook_failures");

            migrationBuilder.DropCheckConstraint(
                name: "CK_webhook_failures_Attempts_Positive",
                schema: "payments",
                table: "webhook_failures");

            migrationBuilder.DropColumn(
                name: "Attempts",
                schema: "payments",
                table: "webhook_failures");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "payments",
                table: "webhook_failures");

            migrationBuilder.DropColumn(
                name: "LastFailedAt",
                schema: "payments",
                table: "webhook_failures");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_failures_IsResolved_OccurredAt",
                schema: "payments",
                table: "webhook_failures",
                columns: new[] { "IsResolved", "OccurredAt" });
        }
    }
}
