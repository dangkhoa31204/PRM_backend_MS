using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRM.Services.Order.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPublicToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicToken",
                table: "Orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValueSql: "replace(gen_random_uuid()::text, '-', '')"); // Backfill mỗi dòng cũ 1 token riêng — tránh đụng unique index

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PublicToken",
                table: "Orders",
                column: "PublicToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_PublicToken",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PublicToken",
                table: "Orders");
        }
    }
}
