using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeConnectMilestoneDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StripeConnectDetailsSubmittedAt",
                schema: "creators",
                table: "creators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StripeConnectPayoutsEnabledAt",
                schema: "creators",
                table: "creators",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripeConnectDetailsSubmittedAt",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "StripeConnectPayoutsEnabledAt",
                schema: "creators",
                table: "creators");
        }
    }
}
