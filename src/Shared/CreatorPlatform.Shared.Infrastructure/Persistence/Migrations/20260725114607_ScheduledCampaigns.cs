using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScheduledCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "marketing",
                table: "campaigns",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledAt",
                schema: "marketing",
                table: "campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_Status_ScheduledAt",
                schema: "marketing",
                table: "campaigns",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_campaigns_Status_ScheduledAt",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                schema: "marketing",
                table: "campaigns");
        }
    }
}
