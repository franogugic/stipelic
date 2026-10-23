using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContactImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImportId",
                schema: "marketing",
                table: "contact_summaries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ImportedAt",
                schema: "marketing",
                table: "contact_summaries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contact_imports",
                schema: "marketing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    ImportedCount = table.Column<int>(type: "integer", nullable: false),
                    ExistingCount = table.Column<int>(type: "integer", nullable: false),
                    DuplicateCount = table.Column<int>(type: "integer", nullable: false),
                    UnsubscribedCount = table.Column<int>(type: "integer", nullable: false),
                    InvalidCount = table.Column<int>(type: "integer", nullable: false),
                    ConsentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConsentAttestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_imports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_imports_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contact_summaries_ImportId",
                schema: "marketing",
                table: "contact_summaries",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_imports_CreatorId_CreatedAt",
                schema: "marketing",
                table: "contact_imports",
                columns: new[] { "CreatorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_imports_PublicId",
                schema: "marketing",
                table: "contact_imports",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_contact_summaries_contact_imports_ImportId",
                schema: "marketing",
                table: "contact_summaries",
                column: "ImportId",
                principalSchema: "marketing",
                principalTable: "contact_imports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contact_summaries_contact_imports_ImportId",
                schema: "marketing",
                table: "contact_summaries");

            migrationBuilder.DropTable(
                name: "contact_imports",
                schema: "marketing");

            migrationBuilder.DropIndex(
                name: "IX_contact_summaries_ImportId",
                schema: "marketing",
                table: "contact_summaries");

            migrationBuilder.DropColumn(
                name: "ImportId",
                schema: "marketing",
                table: "contact_summaries");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                schema: "marketing",
                table: "contact_summaries");
        }
    }
}
