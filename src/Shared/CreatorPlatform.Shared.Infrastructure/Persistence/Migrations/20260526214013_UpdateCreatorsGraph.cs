using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCreatorsGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "creators");

            migrationBuilder.CreateTable(
                name: "creator_plans",
                schema: "creators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MonthlyPriceCents = table.Column<int>(type: "integer", nullable: false),
                    YearlyPriceCents = table.Column<int>(type: "integer", nullable: false),
                    LimitsJson = table.Column<string>(type: "jsonb", nullable: false),
                    FeaturesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creators",
                schema: "creators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_creators_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "creator_members",
                schema: "creators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InvitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_creator_members_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_creator_members_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "creator_settings",
                schema: "creators",
                columns: table => new
                {
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    SupportEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BrandName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_settings", x => x.CreatorId);
                    table.ForeignKey(
                        name: "FK_creator_settings_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_subscriptions",
                schema: "creators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BillingInterval = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProviderSubscriptionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CurrentPeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentPeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TrialEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_creator_subscriptions_creator_plans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "creators",
                        principalTable: "creator_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_creator_subscriptions_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "creators",
                table: "creator_plans",
                columns: new[] { "Id", "Code", "CreatedAt", "Currency", "Description", "FeaturesJson", "IsActive", "LimitsJson", "MonthlyPriceCents", "Name", "PublicId", "SortOrder", "UpdatedAt", "YearlyPriceCents" },
                values: new object[,]
                {
                    { 1, "free", new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Eur", "Start selling with basic creator tools.", "[\n  \"Basic creator workspace\",\n  \"One landing page\",\n  \"One product\",\n  \"Email support\"\n]", true, "{\n  \"contacts\": 500,\n  \"landingPages\": 1,\n  \"products\": 1,\n  \"teamMembers\": 1\n}", 0, "Free", new Guid("2b4d0d95-884d-4f73-8d1f-c21f6a87d901"), 1, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 0 },
                    { 2, "basic", new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Eur", "For creators growing beyond the free tier.", "[\n  \"Everything in Free\",\n  \"More landing pages\",\n  \"More products\",\n  \"Lower platform limits\"\n]", true, "{\n  \"contacts\": 1500,\n  \"landingPages\": 5,\n  \"products\": 5,\n  \"teamMembers\": 2\n}", 1500, "Basic", new Guid("91cbbbf5-6c3d-4e1e-8f24-65bbd47f7296"), 2, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 15000 },
                    { 3, "pro", new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Eur", "For creators selling consistently.", "[\n  \"Everything in Basic\",\n  \"Advanced analytics\",\n  \"More team seats\",\n  \"Priority support\"\n]", true, "{\n  \"contacts\": 5000,\n  \"landingPages\": 20,\n  \"products\": 25,\n  \"teamMembers\": 5\n}", 3000, "Pro", new Guid("fac3d73e-6cfd-41f7-81ff-fd8f2c31e2e7"), 3, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 30000 },
                    { 4, "plus", new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Eur", "For high-volume creators and small teams.", "[\n  \"Everything in Pro\",\n  \"High-volume limits\",\n  \"Team workspace\",\n  \"Premium support\"\n]", true, "{\n  \"contacts\": 20000,\n  \"landingPages\": 100,\n  \"products\": 100,\n  \"teamMembers\": 15\n}", 9900, "Plus", new Guid("74414886-cae9-4e6e-91c6-07b690409f36"), 4, new DateTimeOffset(new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 99000 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_creator_members_CreatorId_UserId",
                schema: "creators",
                table: "creator_members",
                columns: new[] { "CreatorId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creator_members_PublicId",
                schema: "creators",
                table: "creator_members",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creator_members_UserId_Status",
                schema: "creators",
                table: "creator_members",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_creator_plans_Code",
                schema: "creators",
                table: "creator_plans",
                column: "Code",
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

            migrationBuilder.CreateIndex(
                name: "IX_creator_subscriptions_CreatorId_Status",
                schema: "creators",
                table: "creator_subscriptions",
                columns: new[] { "CreatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_creator_subscriptions_PlanId",
                schema: "creators",
                table: "creator_subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_creator_subscriptions_ProviderSubscriptionId",
                schema: "creators",
                table: "creator_subscriptions",
                column: "ProviderSubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creator_subscriptions_PublicId",
                schema: "creators",
                table: "creator_subscriptions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creators_OwnerUserId",
                schema: "creators",
                table: "creators",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_creators_PublicId",
                schema: "creators",
                table: "creators",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creators_Slug",
                schema: "creators",
                table: "creators",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creator_members",
                schema: "creators");

            migrationBuilder.DropTable(
                name: "creator_settings",
                schema: "creators");

            migrationBuilder.DropTable(
                name: "creator_subscriptions",
                schema: "creators");

            migrationBuilder.DropTable(
                name: "creator_plans",
                schema: "creators");

            migrationBuilder.DropTable(
                name: "creators",
                schema: "creators");
        }
    }
}
