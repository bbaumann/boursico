using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleBuyerDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "buyer_city",
                table: "sale",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "buyer_name",
                table: "sale",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "buyer_postal_code",
                table: "sale",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "buyer_city",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "buyer_name",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "buyer_postal_code",
                table: "sale");
        }
    }
}
