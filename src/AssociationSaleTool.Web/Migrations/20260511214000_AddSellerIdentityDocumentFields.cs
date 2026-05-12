using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260511214000_AddSellerIdentityDocumentFields")]
    public partial class AddSellerIdentityDocumentFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identity_document_type",
                table: "seller",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_document_number",
                table: "seller",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "identity_document_issued_on",
                table: "seller",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_document_issuer",
                table: "seller",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "identity_document_type",
                table: "seller");

            migrationBuilder.DropColumn(
                name: "identity_document_number",
                table: "seller");

            migrationBuilder.DropColumn(
                name: "identity_document_issued_on",
                table: "seller");

            migrationBuilder.DropColumn(
                name: "identity_document_issuer",
                table: "seller");
        }
    }
}
