using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    /// <inheritdoc />
    public partial class RequireSellerContactFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "seller",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "postal_address",
                table: "seller",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "postal_address",
                table: "seller");

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "seller",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
