using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSalePaymentTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cheque_bank_name",
                table: "sale",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cheque_number",
                table: "sale",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "sale",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cheque_bank_name",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "cheque_number",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "sale");
        }
    }
}
