using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorStripeCustomerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                schema: "creators",
                table: "creators",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_creators_StripeCustomerId",
                schema: "creators",
                table: "creators",
                column: "StripeCustomerId",
                unique: true,
                filter: "\"StripeCustomerId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_creators_StripeCustomerId",
                schema: "creators",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                schema: "creators",
                table: "creators");
        }
    }
}
