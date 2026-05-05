using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260505090000_AddSellerPostalCodeAndCity")]
    public partial class AddSellerPostalCodeAndCity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "postal_code",
                table: "seller",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "seller",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "postal_code",
                table: "seller");

            migrationBuilder.DropColumn(
                name: "city",
                table: "seller");
        }
    }
}
