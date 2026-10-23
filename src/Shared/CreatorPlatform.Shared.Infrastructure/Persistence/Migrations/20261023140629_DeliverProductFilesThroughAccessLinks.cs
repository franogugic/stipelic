using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <summary>F8.1: every product is a file. Drops product types and AccessUrl, adds the product's document and the
    /// personal links (products.access_grants) for buyers and lead sign-ups. An Active product needs its file, so the
    /// Active products that exist now (none has one) become Drafts — pre-launch demo data only.</summary>
    public partial class DeliverProductFilesThroughAccessLinks : Migration
    {
        /// <summary>Idempotent. Public so the integration tests run the exact statement.</summary>
        public const string DraftActiveProductsWithoutFileSql = """
            UPDATE products.products
            SET "Status" = 'Draft', "UpdatedAt" = now()
            WHERE "Status" = 'Active' AND "FileBlobPath" IS NULL;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessUrl",
                schema: "products",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "products",
                table: "products");

            migrationBuilder.AddColumn<string>(
                name: "FileBlobPath",
                schema: "products",
                table: "products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileContentType",
                schema: "products",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                schema: "products",
                table: "products",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                schema: "products",
                table: "products",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FileUploadedAt",
                schema: "products",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ToEmail",
                schema: "email",
                table: "email_outbox_messages",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "BlobPath",
                schema: "media",
                table: "blob_references",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                schema: "media",
                table: "blob_references",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "access_grants",
                schema: "products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    EmailCaptureId = table.Column<Guid>(type: "uuid", nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DownloadCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastDownloadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_grants", x => x.Id);
                    table.CheckConstraint("CK_access_grants_One_Source", "(\"OrderId\" IS NOT NULL AND \"EmailCaptureId\" IS NULL) OR (\"OrderId\" IS NULL AND \"EmailCaptureId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_access_grants_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "creators",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_access_grants_email_captures_EmailCaptureId",
                        column: x => x.EmailCaptureId,
                        principalSchema: "analytics",
                        principalTable: "email_captures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_access_grants_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_access_grants_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "products",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(DraftActiveProductsWithoutFileSql);

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_Active_Has_File",
                schema: "products",
                table: "products",
                sql: "\"Status\" <> 'Active' OR \"FileBlobPath\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_products_File_Complete",
                schema: "products",
                table: "products",
                sql: "(\"FileBlobPath\" IS NULL AND \"FileName\" IS NULL AND \"FileSizeBytes\" IS NULL AND \"FileContentType\" IS NULL AND \"FileUploadedAt\" IS NULL) OR (\"FileBlobPath\" IS NOT NULL AND \"FileName\" IS NOT NULL AND \"FileSizeBytes\" > 0 AND \"FileContentType\" IS NOT NULL AND \"FileUploadedAt\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_access_grants_CreatorId_ProductId",
                schema: "products",
                table: "access_grants",
                columns: new[] { "CreatorId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_access_grants_EmailCaptureId_ProductId",
                schema: "products",
                table: "access_grants",
                columns: new[] { "EmailCaptureId", "ProductId" },
                unique: true,
                filter: "\"EmailCaptureId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_access_grants_OrderId",
                schema: "products",
                table: "access_grants",
                column: "OrderId",
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_access_grants_ProductId",
                schema: "products",
                table: "access_grants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_access_grants_Token",
                schema: "products",
                table: "access_grants",
                column: "Token",
                unique: true);
        }

        /// <summary>Products come back without a type (all "Digital") or link; the Drafts made by Up stay Drafts.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_grants",
                schema: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_Active_Has_File",
                schema: "products",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_products_File_Complete",
                schema: "products",
                table: "products");

            migrationBuilder.DropColumn(
                name: "FileBlobPath",
                schema: "products",
                table: "products");

            migrationBuilder.DropColumn(
                name: "FileContentType",
                schema: "products",
                table: "products");

            migrationBuilder.DropColumn(
                name: "FileName",
                schema: "products",
                table: "products");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                schema: "products",
                table: "products");

            migrationBuilder.DropColumn(
                name: "FileUploadedAt",
                schema: "products",
                table: "products");

            migrationBuilder.DropColumn(
                name: "BlobPath",
                schema: "media",
                table: "blob_references");

            migrationBuilder.DropColumn(
                name: "FileName",
                schema: "media",
                table: "blob_references");

            migrationBuilder.AddColumn<string>(
                name: "AccessUrl",
                schema: "products",
                table: "products",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "products",
                table: "products",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Digital");

            migrationBuilder.AlterColumn<string>(
                name: "ToEmail",
                schema: "email",
                table: "email_outbox_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);
        }
    }
}
