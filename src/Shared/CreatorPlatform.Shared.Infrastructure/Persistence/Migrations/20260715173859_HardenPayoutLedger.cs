using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenPayoutLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_PayoutId",
                schema: "payouts",
                table: "ledger_entries");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StripeConnectStatusEventAt",
                schema: "creators",
                table: "creators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PayoutId_Type",
                schema: "payouts",
                table: "ledger_entries",
                columns: new[] { "PayoutId", "Type" },
                unique: true,
                filter: "\"PayoutId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_PayoutId_Type",
                schema: "payouts",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "StripeConnectStatusEventAt",
                schema: "creators",
                table: "creators");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PayoutId",
                schema: "payouts",
                table: "ledger_entries",
                column: "PayoutId",
                filter: "\"PayoutId\" IS NOT NULL");
        }
    }
}
