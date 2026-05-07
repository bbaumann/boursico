using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "invoice_issued_at",
                table: "sale",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_number",
                table: "sale",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_invoice_number",
                table: "sale",
                column: "invoice_number",
                unique: true,
                filter: "invoice_number IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sale_invoice_number",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "invoice_issued_at",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "invoice_number",
                table: "sale");
        }
    }
}
