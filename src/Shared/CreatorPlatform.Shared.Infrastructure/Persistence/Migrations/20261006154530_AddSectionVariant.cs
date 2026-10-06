using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <summary>Sections store their layout (variant) as a column, and BackgroundColor may be null for "page
    /// default". Existing sections get the first variant of their type, matching SectionTemplates.GetDefault;
    /// their hex backgrounds are kept.</summary>
    public partial class AddSectionVariant : Migration
    {
        /// <summary>Public so an integration test can check it against SectionTemplates.GetDefault.</summary>
        public const string BackfillVariantsSql = """
            UPDATE landing_pages.landing_page_sections SET "Variant" = CASE "Type"
                WHEN 'Navbar' THEN 'simple'
                WHEN 'Hero' THEN 'split'
                WHEN 'Features' THEN 'numbered'
                WHEN 'ProductDetails' THEN 'image-left'
                WHEN 'Testimonials' THEN 'cards'
                WHEN 'Faq' THEN 'accordion'
                WHEN 'Gallery' THEN 'grid'
                WHEN 'Cta' THEN 'banner'
                WHEN 'Footer' THEN 'simple'
            END
            WHERE "Variant" = '';
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BackgroundColor",
                schema: "landing_pages",
                table: "landing_page_sections",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(7)",
                oldMaxLength: 7);

            migrationBuilder.AddColumn<string>(
                name: "Variant",
                schema: "landing_pages",
                table: "landing_page_sections",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(BackfillVariantsSql);
            migrationBuilder.Sql("""
                ALTER TABLE landing_pages.landing_page_sections ALTER COLUMN "Variant" DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Variant",
                schema: "landing_pages",
                table: "landing_page_sections");

            migrationBuilder.Sql("""
                UPDATE landing_pages.landing_page_sections SET "BackgroundColor" = '#ffffff' WHERE "BackgroundColor" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "BackgroundColor",
                schema: "landing_pages",
                table: "landing_page_sections",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(7)",
                oldMaxLength: 7,
                oldNullable: true);
        }
    }
}
