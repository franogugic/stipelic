using System;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CreatorPlatformDbContext))]
    [Migration("20260528220556_HardenCreatorEndpoints")]
    public partial class HardenCreatorEndpoints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_creators_OwnerUserId",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropIndex(
                name: "IX_creators_Slug",
                schema: "creators",
                table: "creators");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "creators",
                table: "creator_plans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "creators",
                table: "creator_plans",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Timezone",
                schema: "creators",
                table: "creator_settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "BrandName",
                schema: "creators",
                table: "creator_settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                schema: "creators",
                table: "creators",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "creators",
                table: "creators",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultCurrency",
                schema: "creators",
                table: "creators",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.CreateIndex(
                name: "IX_creators_OwnerUserId",
                schema: "creators",
                table: "creators",
                column: "OwnerUserId",
                unique: true,
                filter: "\"Status\" <> 'Disabled'");

            migrationBuilder.CreateIndex(
                name: "IX_creators_Slug",
                schema: "creators",
                table: "creators",
                column: "Slug",
                unique: true,
                filter: "\"Status\" <> 'Disabled'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_creators_OwnerUserId",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropIndex(
                name: "IX_creators_Slug",
                schema: "creators",
                table: "creators");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "creators",
                table: "creator_plans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "creators",
                table: "creator_plans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Timezone",
                schema: "creators",
                table: "creator_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "BrandName",
                schema: "creators",
                table: "creator_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                schema: "creators",
                table: "creators",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "creators",
                table: "creators",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultCurrency",
                schema: "creators",
                table: "creators",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5);

            migrationBuilder.CreateIndex(
                name: "IX_creators_OwnerUserId",
                schema: "creators",
                table: "creators",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creators_Slug",
                schema: "creators",
                table: "creators",
                column: "Slug",
                unique: true);
        }
    }
}
