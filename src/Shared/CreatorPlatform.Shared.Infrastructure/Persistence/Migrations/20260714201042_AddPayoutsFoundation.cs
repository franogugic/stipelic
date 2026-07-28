using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payouts");

            migrationBuilder.AddColumn<string>(
                name: "PayoutMode",
                schema: "orders",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // Existing rows land on the '' placeholder above (Postgres applies a constant DEFAULT to
            // pre-existing rows too) — replace it with the real backfill value before anything reads them
            // back through the HasConversion<string>() enum mapping (an empty string won't parse).
            migrationBuilder.Sql("""
                UPDATE orders.orders
                SET "PayoutMode" = 'BankTransfer'
                WHERE "PayoutMode" = '';
                """);

            migrationBuilder.AddColumn<int>(
                name: "PlatformFeeBasisPoints",
                schema: "orders",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlatformFeeCents",
                schema: "orders",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                schema: "creators",
                table: "creators",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayoutMode",
                schema: "creators",
                table: "creators",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // Same reasoning as the orders.PayoutMode backfill above — replace the '' placeholder on
            // existing rows with real values (HR / BankTransfer, matching CreatorService's temporary
            // default until Task 2.5 makes country a real signup field) before anything reads them back.
            migrationBuilder.Sql("""
                UPDATE creators.creators
                SET "CountryCode" = 'HR', "PayoutMode" = 'BankTransfer'
                WHERE "CountryCode" = '';
                """);

            migrationBuilder.AddColumn<string>(
                name: "StripeConnectAccountId",
                schema: "creators",
                table: "creators",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StripeConnectChargesEnabled",
                schema: "creators",
                table: "creators",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StripeConnectDetailsSubmitted",
                schema: "creators",
                table: "creators",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StripeConnectPayoutsEnabled",
                schema: "creators",
                table: "creators",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "creator_payout_profiles",
                schema: "creators",
                columns: table => new
                {
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    AccountHolderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    BankCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_payout_profiles", x => x.CreatorId);
                    table.ForeignKey(
                        name: "FK_creator_payout_profiles_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payouts",
                schema: "payouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BankReference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payouts", x => x.Id);
                    table.CheckConstraint("CK_payouts_AmountCents_Positive", "\"AmountCents\" > 0");
                    table.ForeignKey(
                        name: "FK_payouts_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                schema: "payouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    PayoutId = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.Id);
                    table.CheckConstraint("CK_ledger_entries_AmountCents_NotZero", "\"AmountCents\" <> 0");
                    table.ForeignKey(
                        name: "FK_ledger_entries_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalSchema: "payouts",
                        principalTable: "payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_orders_PlatformFeeCents_NonNegative",
                schema: "orders",
                table: "orders",
                sql: "\"PlatformFeeCents\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_creators_StripeConnectAccountId",
                schema: "creators",
                table: "creators",
                column: "StripeConnectAccountId",
                unique: true,
                filter: "\"StripeConnectAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_CreatorId_CreatedAt",
                schema: "payouts",
                table: "ledger_entries",
                columns: new[] { "CreatorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_OrderId_Type",
                schema: "payouts",
                table: "ledger_entries",
                columns: new[] { "OrderId", "Type" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PayoutId",
                schema: "payouts",
                table: "ledger_entries",
                column: "PayoutId",
                filter: "\"PayoutId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PublicId",
                schema: "payouts",
                table: "ledger_entries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payouts_CreatorId_CreatedAt",
                schema: "payouts",
                table: "payouts",
                columns: new[] { "CreatorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payouts_PublicId",
                schema: "payouts",
                table: "payouts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payouts_Status",
                schema: "payouts",
                table: "payouts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creator_payout_profiles",
                schema: "creators");

            migrationBuilder.DropTable(
                name: "ledger_entries",
                schema: "payouts");

            migrationBuilder.DropTable(
                name: "payouts",
                schema: "payouts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_orders_PlatformFeeCents_NonNegative",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_creators_StripeConnectAccountId",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "PayoutMode",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PlatformFeeBasisPoints",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PlatformFeeCents",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "PayoutMode",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "StripeConnectAccountId",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "StripeConnectChargesEnabled",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "StripeConnectDetailsSubmitted",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "StripeConnectPayoutsEnabled",
                schema: "creators",
                table: "creators");
        }
    }
}
