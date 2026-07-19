using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOutboxReplyToAndUnsubscribeUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ListUnsubscribeUrl",
                schema: "email",
                table: "email_outbox_messages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyTo",
                schema: "email",
                table: "email_outbox_messages",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ListUnsubscribeUrl",
                schema: "email",
                table: "email_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ReplyTo",
                schema: "email",
                table: "email_outbox_messages");
        }
    }
}
