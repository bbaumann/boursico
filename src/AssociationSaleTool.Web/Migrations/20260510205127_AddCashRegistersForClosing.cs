using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCashRegistersForClosing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_register",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    initial_cash_cents = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_register", x => x.Id);
                    table.CheckConstraint("ck_cash_register_initial_cash_cents", "initial_cash_cents >= 0");
                    table.CheckConstraint("ck_cash_register_is_active", "is_active IN (0, 1)");
                    table.CheckConstraint("ck_cash_register_name_not_blank", "length(trim(name)) > 0");
                });

            migrationBuilder.AddColumn<long>(
                name: "refund_cash_register_id",
                table: "sale_line",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cash_register_id",
                table: "sale",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_cash_register_name",
                table: "cash_register",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_refund_cash_register_id",
                table: "sale_line",
                column: "refund_cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_cash_register_id",
                table: "sale",
                column: "cash_register_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sale_line_refund_cash_register_id",
                table: "sale_line");

            migrationBuilder.DropIndex(
                name: "IX_sale_cash_register_id",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "refund_cash_register_id",
                table: "sale_line");

            migrationBuilder.DropColumn(
                name: "cash_register_id",
                table: "sale");

            migrationBuilder.DropTable(
                name: "cash_register");
        }
    }
}
