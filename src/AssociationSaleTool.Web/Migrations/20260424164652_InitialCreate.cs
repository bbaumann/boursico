using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sale",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    sold_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    total_amount_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    receipt_details = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale", x => x.Id);
                    table.CheckConstraint("ck_sale_total_amount_cents", "total_amount_cents >= 0");
                });

            migrationBuilder.CreateTable(
                name: "seller",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    seller_code = table.Column<string>(type: "TEXT", nullable: false),
                    first_name = table.Column<string>(type: "TEXT", nullable: false),
                    last_name = table.Column<string>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", nullable: true),
                    phone = table.Column<string>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "item",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    seller_id = table.Column<long>(type: "INTEGER", nullable: false),
                    reference = table.Column<string>(type: "TEXT", nullable: false),
                    item_number = table.Column<int>(type: "INTEGER", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    seller_price_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    sale_price_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item", x => x.Id);
                    table.CheckConstraint("ck_item_sale_price_cents", "sale_price_cents >= 0");
                    table.CheckConstraint("ck_item_seller_price_cents", "seller_price_cents >= 0");
                    table.CheckConstraint("ck_item_status", "status IN ('registered', 'in_stock', 'sold', 'missing')");
                    table.ForeignKey(
                        name: "FK_item_seller_seller_id",
                        column: x => x.seller_id,
                        principalTable: "seller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_line",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    sale_id = table.Column<long>(type: "INTEGER", nullable: false),
                    item_id = table.Column<long>(type: "INTEGER", nullable: false),
                    seller_price_at_sale_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    sale_price_at_sale_cents = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_line", x => x.Id);
                    table.CheckConstraint("ck_sale_line_sale_price_cents", "sale_price_at_sale_cents >= 0");
                    table.CheckConstraint("ck_sale_line_seller_price_cents", "seller_price_at_sale_cents >= 0");
                    table.ForeignKey(
                        name: "FK_sale_line_item_item_id",
                        column: x => x.item_id,
                        principalTable: "item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_line_sale_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_item_reference",
                table: "item",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_seller_id",
                table: "item",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_seller_id_item_number",
                table: "item",
                columns: new[] { "seller_id", "item_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_status",
                table: "item",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_item_title",
                table: "item",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "IX_sale_sold_at",
                table: "sale",
                column: "sold_at");

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_item_id",
                table: "sale_line",
                column: "item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_sale_id",
                table: "sale_line",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "IX_seller_seller_code",
                table: "seller",
                column: "seller_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sale_line");

            migrationBuilder.DropTable(
                name: "item");

            migrationBuilder.DropTable(
                name: "sale");

            migrationBuilder.DropTable(
                name: "seller");
        }
    }
}
