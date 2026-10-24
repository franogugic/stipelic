using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowPurgingSentEmailBodies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PlainTextBody",
                schema: "email",
                table: "email_outbox_messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "HtmlBody",
                schema: "email",
                table: "email_outbox_messages",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_SentAt_WithBodies",
                schema: "email",
                table: "email_outbox_messages",
                column: "SentAt",
                filter: "\"Status\" = 'Sent' AND \"HtmlBody\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_outbox_messages_SentAt_WithBodies",
                schema: "email",
                table: "email_outbox_messages");

            // Purged bodies come back as empty strings: NOT NULL cannot be restored over NULLs.
            migrationBuilder.Sql("""
                UPDATE email.email_outbox_messages
                SET "HtmlBody" = COALESCE("HtmlBody", ''), "PlainTextBody" = COALESCE("PlainTextBody", '')
                WHERE "HtmlBody" IS NULL OR "PlainTextBody" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PlainTextBody",
                schema: "email",
                table: "email_outbox_messages",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HtmlBody",
                schema: "email",
                table: "email_outbox_messages",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
