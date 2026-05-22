using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOutboxProcessingLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProcessingExpiresAt",
                schema: "email",
                table: "email_outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_outbox_messages_Status_ProcessingExpiresAt",
                schema: "email",
                table: "email_outbox_messages",
                columns: new[] { "Status", "ProcessingExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_outbox_messages_Status_ProcessingExpiresAt",
                schema: "email",
                table: "email_outbox_messages");

            migrationBuilder.DropColumn(
                name: "ProcessingExpiresAt",
                schema: "email",
                table: "email_outbox_messages");
        }
    }
}
