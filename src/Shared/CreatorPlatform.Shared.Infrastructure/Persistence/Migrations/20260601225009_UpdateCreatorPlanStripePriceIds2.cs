using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCreatorPlanStripePriceIds2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 2,
                column: "StripePriceId",
                value: "price_1TdeWaV05qi4vb2Az5izI6wj");

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 3,
                column: "StripePriceId",
                value: "price_1TdeXhV05qi4vb2A1eMRjOFC");

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 4,
                column: "StripePriceId",
                value: "price_1TdeXtV05qi4vb2AGEHnliO9");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 2,
                column: "StripePriceId",
                value: "price_1Tch5JV05qi4vb2A7qhjOae3");

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 3,
                column: "StripePriceId",
                value: "price_1Tch5WV05qi4vb2ATVhLgp5V");

            migrationBuilder.UpdateData(
                schema: "creators",
                table: "creator_plans",
                keyColumn: "Id",
                keyValue: 4,
                column: "StripePriceId",
                value: "price_1Tch5iV05qi4vb2AJYWxprid");
        }
    }
}
