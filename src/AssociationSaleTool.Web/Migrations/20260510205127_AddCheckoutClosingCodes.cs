using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutClosingCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "refund_checkout_code",
                table: "sale_line",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "checkout_code",
                table: "sale",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_refund_checkout_code",
                table: "sale_line",
                column: "refund_checkout_code");

            migrationBuilder.CreateIndex(
                name: "IX_sale_checkout_code",
                table: "sale",
                column: "checkout_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sale_line_refund_checkout_code",
                table: "sale_line");

            migrationBuilder.DropIndex(
                name: "IX_sale_checkout_code",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "refund_checkout_code",
                table: "sale_line");

            migrationBuilder.DropColumn(
                name: "checkout_code",
                table: "sale");
        }
    }
}
