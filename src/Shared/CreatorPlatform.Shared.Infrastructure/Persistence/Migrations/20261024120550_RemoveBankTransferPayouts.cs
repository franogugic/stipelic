using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBankTransferPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The payout-request email is gone with its purpose; an unsent row would no longer map to the enum.
            migrationBuilder.Sql("DELETE FROM email.email_outbox_messages WHERE \"Purpose\" = 'PayoutRequested';");

            migrationBuilder.DropTable(
                name: "creator_payout_profiles",
                schema: "creators");

            migrationBuilder.DropTable(
                name: "ledger_entries",
                schema: "payouts");

            migrationBuilder.DropTable(
                name: "payouts",
                schema: "payouts");

            migrationBuilder.DropColumn(
                name: "PayoutMode",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PayoutMode",
                schema: "creators",
                table: "creators");

            migrationBuilder.Sql("DROP SCHEMA IF EXISTS payouts;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                defaultValue: "StripeConnect");

            migrationBuilder.AddColumn<string>(
                name: "PayoutMode",
                schema: "creators",
                table: "creators",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "StripeConnect");

            migrationBuilder.CreateTable(
                name: "creator_payout_profiles",
                schema: "creators",
                columns: table => new
                {
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    AccountHolderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BankCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
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
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    BankReference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    AmountCents = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    PayoutId = table.Column<int>(type: "integer", nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundedToCents = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.Id);
                    table.CheckConstraint("CK_ledger_entries_AmountCents_NotZero", "\"AmountCents\" <> 0");
                    table.CheckConstraint("CK_ledger_entries_RefundedToCents_OnRefundsOnly", "(\"Type\" IN ('RefundDebit', 'FeeRefundCredit')) = (\"RefundedToCents\" IS NOT NULL)");
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

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_CreatorId_CreatedAt",
                schema: "payouts",
                table: "ledger_entries",
                columns: new[] { "CreatorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_OrderId_Type_RefundedToCents",
                schema: "payouts",
                table: "ledger_entries",
                columns: new[] { "OrderId", "Type", "RefundedToCents" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PayoutId_Type",
                schema: "payouts",
                table: "ledger_entries",
                columns: new[] { "PayoutId", "Type" },
                unique: true,
                filter: "\"PayoutId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PublicId",
                schema: "payouts",
                table: "ledger_entries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payouts_CreatorId",
                schema: "payouts",
                table: "payouts",
                column: "CreatorId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

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
    }
}
