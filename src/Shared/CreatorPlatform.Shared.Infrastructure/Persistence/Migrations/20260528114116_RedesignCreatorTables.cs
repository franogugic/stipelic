using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedesignCreatorTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_creators_OwnerUserId",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropIndex(
                name: "IX_creator_subscriptions_PublicId",
                schema: "creators",
                table: "creator_subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_creator_plans_IsActive_SortOrder",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.DropIndex(
                name: "IX_creator_plans_PublicId",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.DropColumn(
                name: "CancelAt",
                schema: "creators",
                table: "creator_subscriptions");

            migrationBuilder.DropColumn(
                name: "PublicId",
                schema: "creators",
                table: "creator_subscriptions");

            migrationBuilder.DropColumn(
                name: "FeaturesJson",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.DropColumn(
                name: "LimitsJson",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.DropColumn(
                name: "PublicId",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.DropColumn(
                name: "YearlyPriceCents",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.RenameColumn(
                name: "MonthlyPriceCents",
                schema: "creators",
                table: "creator_plans",
                newName: "PriceCents");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "creators",
                table: "creator_settings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "creators",
                table: "creator_plans",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "BillingInterval",
                schema: "creators",
                table: "creator_plans",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<int>(
                name: "PlatformFeeBasisPoints",
                schema: "creators",
                table: "creator_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "creators",
                table: "creator_plans",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.CreateTable(
                name: "creator_plan_limits",
                schema: "creators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    LimitKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LimitValue = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_plan_limits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_creator_plan_limits_creator_plans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "creators",
                        principalTable: "creator_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_usage_counters",
                schema: "creators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    UsageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsedValue = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_usage_counters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_creator_usage_counters_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "creators",
                table: "creator_plan_limits",
                columns: new[] { "Id", "CreatedAt", "LimitKey", "LimitValue", "PlanId", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_landing_pages", 1, 1, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 2, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_products", 1, 1, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 3, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_members", 1, 1, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 4, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_email_sends_per_month", 500, 1, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 5, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_contacts", 500, 1, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 6, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_landing_pages", 5, 2, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 7, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_products", 5, 2, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 8, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_members", 2, 2, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 9, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_email_sends_per_month", 1500, 2, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 10, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_contacts", 1500, 2, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 11, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_landing_pages", 20, 3, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 12, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_products", 25, 3, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 13, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_members", 5, 3, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 14, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_email_sends_per_month", 5000, 3, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 15, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_contacts", 5000, 3, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 16, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_landing_pages", 100, 4, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 17, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_products", 100, 4, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 18, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_members", 15, 4, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 19, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_email_sends_per_month", -1, 4, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 20, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "max_contacts", -1, 4, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "BillingInterval", "Description", "PlatformFeeBasisPoints", "Status" },
                values: new object[] { "None", "For creators with up to 500 contacts.", 1000, "Active" });

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "BillingInterval", "Description", "PlatformFeeBasisPoints", "PriceCents", "Status" },
                values: new object[] { "Monthly", "For creators with up to 1,500 contacts.", 500, 1500, "Active" });

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "BillingInterval", "Description", "PlatformFeeBasisPoints", "PriceCents", "Status" },
                values: new object[] { "Monthly", "For creators with up to 5,000 contacts.", 250, 3000, "Active" });

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "BillingInterval", "Description", "Name", "PlatformFeeBasisPoints", "PriceCents", "Status" },
                values: new object[] { "Monthly", "For creators above 5,000 contacts.", "Pro Plus", 100, 9900, "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_creators_OwnerUserId",
                schema: "creators",
                table: "creators",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creator_plans_Status",
                schema: "creators",
                table: "creator_plans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_creator_members_CreatorId_Status",
                schema: "creators",
                table: "creator_members",
                columns: new[] { "CreatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_creator_plan_limits_PlanId_LimitKey",
                schema: "creators",
                table: "creator_plan_limits",
                columns: new[] { "PlanId", "LimitKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creator_usage_counters_CreatorId_UsageKey",
                schema: "creators",
                table: "creator_usage_counters",
                columns: new[] { "CreatorId", "UsageKey" });

            migrationBuilder.CreateIndex(
                name: "IX_creator_usage_counters_CreatorId_UsageKey_PeriodStart_Perio~",
                schema: "creators",
                table: "creator_usage_counters",
                columns: new[] { "CreatorId", "UsageKey", "PeriodStart", "PeriodEnd" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creator_plan_limits",
                schema: "creators");

            migrationBuilder.DropTable(
                name: "creator_usage_counters",
                schema: "creators");

            migrationBuilder.DropIndex(
                name: "IX_creators_OwnerUserId",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropIndex(
                name: "IX_creator_plans_Status",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.DropIndex(
                name: "IX_creator_members_CreatorId_Status",
                schema: "creators",
                table: "creator_members");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "creators",
                table: "creator_settings");

            migrationBuilder.DropColumn(
                name: "BillingInterval",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.RenameColumn(
                name: "PriceCents",
                schema: "creators",
                table: "creator_plans",
                newName: "MonthlyPriceCents");

            migrationBuilder.DropColumn(
                name: "PlatformFeeBasisPoints",
                schema: "creators",
                table: "creator_plans");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelAt",
                schema: "creators",
                table: "creator_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                schema: "creators",
                table: "creator_subscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "creators",
                table: "creator_plans",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeaturesJson",
                schema: "creators",
                table: "creator_plans",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "creators",
                table: "creator_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LimitsJson",
                schema: "creators",
                table: "creator_plans",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "creators",
                table: "creator_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                schema: "creators",
                table: "creator_plans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "YearlyPriceCents",
                schema: "creators",
                table: "creator_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FeaturesJson", "IsActive", "LimitsJson", "MonthlyPriceCents", "PublicId", "SortOrder" },
                values: new object[] { "[\n  \"Basic creator workspace\",\n  \"One landing page\",\n  \"One product\",\n  \"Email support\"\n]", true, "{\n  \"contacts\": 500,\n  \"landingPages\": 1,\n  \"products\": 1,\n  \"teamMembers\": 1\n}", 0, new Guid("2b4d0d95-884d-4f73-8d1f-c21f6a87d901"), 1 });

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "FeaturesJson", "IsActive", "LimitsJson", "MonthlyPriceCents", "PublicId", "SortOrder", "YearlyPriceCents" },
                values: new object[] { "[\n  \"Everything in Free\",\n  \"More landing pages\",\n  \"More products\",\n  \"Lower platform limits\"\n]", true, "{\n  \"contacts\": 1500,\n  \"landingPages\": 5,\n  \"products\": 5,\n  \"teamMembers\": 2\n}", 1500, new Guid("91cbbbf5-6c3d-4e1e-8f24-65bbd47f7296"), 2, 15000 });

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "FeaturesJson", "IsActive", "LimitsJson", "MonthlyPriceCents", "PublicId", "SortOrder", "YearlyPriceCents" },
                values: new object[] { "[\n  \"Everything in Basic\",\n  \"Advanced analytics\",\n  \"More team seats\",\n  \"Priority support\"\n]", true, "{\n  \"contacts\": 5000,\n  \"landingPages\": 20,\n  \"products\": 25,\n  \"teamMembers\": 5\n}", 3000, new Guid("fac3d73e-6cfd-41f7-81ff-fd8f2c31e2e7"), 3, 30000 });

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "FeaturesJson", "IsActive", "LimitsJson", "MonthlyPriceCents", "PublicId", "SortOrder", "YearlyPriceCents" },
                values: new object[] { "[\n  \"Everything in Pro\",\n  \"High-volume limits\",\n  \"Team workspace\",\n  \"Premium support\"\n]", true, "{\n  \"contacts\": 20000,\n  \"landingPages\": 100,\n  \"products\": 100,\n  \"teamMembers\": 15\n}", 9900, new Guid("74414886-cae9-4e6e-91c6-07b690409f36"), 4, 99000 });

            migrationBuilder.CreateIndex(
                name: "IX_creators_OwnerUserId",
                schema: "creators",
                table: "creators",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_creator_subscriptions_PublicId",
                schema: "creators",
                table: "creator_subscriptions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creator_plans_IsActive_SortOrder",
                schema: "creators",
                table: "creator_plans",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_creator_plans_PublicId",
                schema: "creators",
                table: "creator_plans",
                column: "PublicId",
                unique: true);
        }
    }
}
