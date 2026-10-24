using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeleteWorkspacesForReal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OwnerUserId",
                schema: "creators",
                table: "creators",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "creators",
                table: "creators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FilesDeletedAt",
                schema: "creators",
                table: "creators",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fails while tombstones exist: they belong to no one.
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "FilesDeletedAt",
                schema: "creators",
                table: "creators");

            migrationBuilder.AlterColumn<int>(
                name: "OwnerUserId",
                schema: "creators",
                table: "creators",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
