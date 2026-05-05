using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260505093000_RenameSaleReceiptDetailsToBuyerAddress")]
    public partial class RenameSaleReceiptDetailsToBuyerAddress : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "receipt_details",
                table: "sale",
                newName: "buyer_address");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "buyer_address",
                table: "sale",
                newName: "receipt_details");
        }
    }
}
