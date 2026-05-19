using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CreatorPlatform.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixRoleIdEnumMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "auth",
                table: "roles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { (short)1, "user" },
                    { (short)2, "platform_admin" },
                    { (short)3, "creator_owner" },
                    { (short)4, "creator_staff" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "auth",
                table: "roles",
                keyColumn: "Id",
                keyValue: (short)1);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "roles",
                keyColumn: "Id",
                keyValue: (short)2);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "roles",
                keyColumn: "Id",
                keyValue: (short)3);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "roles",
                keyColumn: "Id",
                keyValue: (short)4);
        }
    }
}
