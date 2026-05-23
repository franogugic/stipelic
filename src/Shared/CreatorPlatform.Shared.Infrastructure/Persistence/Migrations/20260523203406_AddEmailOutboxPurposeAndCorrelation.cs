using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOutboxPurposeAndCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationKey",
                schema: "email",
                table: "email_outbox_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "email",
                table: "email_outbox_messages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_Purpose_CorrelationKey_Status",
                schema: "email",
                table: "email_outbox_messages",
                columns: new[] { "Purpose", "CorrelationKey", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_outbox_messages_Purpose_CorrelationKey_Status",
                schema: "email",
                table: "email_outbox_messages");

            migrationBuilder.DropColumn(
                name: "CorrelationKey",
                schema: "email",
                table: "email_outbox_messages");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "email",
                table: "email_outbox_messages");
        }
    }
}
