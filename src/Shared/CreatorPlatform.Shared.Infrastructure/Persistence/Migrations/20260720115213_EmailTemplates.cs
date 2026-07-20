using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TemplateId",
                schema: "marketing",
                table: "campaigns",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "email_templates",
                schema: "marketing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    CtaLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CtaUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_templates_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_TemplateId",
                schema: "marketing",
                table: "campaigns",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_email_templates_CreatorId_Status",
                schema: "marketing",
                table: "email_templates",
                columns: new[] { "CreatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_email_templates_PublicId",
                schema: "marketing",
                table: "email_templates",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_campaigns_email_templates_TemplateId",
                schema: "marketing",
                table: "campaigns",
                column: "TemplateId",
                principalSchema: "marketing",
                principalTable: "email_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 02R rework: the old "Draft campaign with inline content" model is gone — every pre-existing
            // Draft row becomes a reusable template instead (Name defaults to its Subject, since Drafts
            // never had a separate name) and is then removed. Draft campaigns never reached the send
            // pipeline, so they never had campaign_recipients rows to worry about. Existing Queued
            // campaigns are untouched (TemplateId stays NULL — "legacy inline send", per the domain
            // comment on Campaign.TemplateId).
            migrationBuilder.Sql("""
                INSERT INTO marketing.email_templates
                    ("PublicId", "CreatorId", "Name", "Subject", "BodyText", "CtaLabel", "CtaUrl", "Status", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), "CreatorId", "Subject", "Subject", "BodyText", "CtaLabel", "CtaUrl", 'Active', "CreatedAt", "UpdatedAt"
                FROM marketing.campaigns
                WHERE "Status" = 'Draft';

                DELETE FROM marketing.campaigns WHERE "Status" = 'Draft';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_campaigns_email_templates_TemplateId",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropTable(
                name: "email_templates",
                schema: "marketing");

            migrationBuilder.DropIndex(
                name: "IX_campaigns_TemplateId",
                schema: "marketing",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                schema: "marketing",
                table: "campaigns");
        }
    }
}
