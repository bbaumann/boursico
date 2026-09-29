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

            migrationBuilder.CreateTable(
                name: "event_state",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    sale_phase = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_state", x => x.Id);
                    table.CheckConstraint("ck_event_state_sale_phase", "sale_phase IN ('presale', 'sale', 'post_sale')");
                    table.CheckConstraint("ck_event_state_singleton", "Id = 1");
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
                    phone = table.Column<string>(type: "TEXT", nullable: false),
                    postal_address = table.Column<string>(type: "TEXT", nullable: false),
                    postal_code = table.Column<string>(type: "TEXT", nullable: false),
                    city = table.Column<string>(type: "TEXT", nullable: false),
                    identity_document_type = table.Column<string>(type: "TEXT", nullable: true),
                    identity_document_number = table.Column<string>(type: "TEXT", nullable: true),
                    identity_document_issued_on = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    fee_paid_cents = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    deposit_status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "deposit_pending"),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    import_notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seller", x => x.Id);
                    table.CheckConstraint("ck_seller_deposit_status", "deposit_status IN ('deposit_pending', 'deposit_validated')");
                    table.CheckConstraint("ck_seller_fee_paid_cents", "fee_paid_cents >= 0");
                });

            migrationBuilder.CreateTable(
                name: "sale",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    sold_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    subtotal_seller_price_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    checkout_fee_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    total_paid_cents = table.Column<int>(type: "INTEGER", nullable: false),
                    cash_register_id = table.Column<long>(type: "INTEGER", nullable: false),
                    buyer_name = table.Column<string>(type: "TEXT", nullable: true),
                    buyer_postal_code = table.Column<string>(type: "TEXT", nullable: true),
                    buyer_city = table.Column<string>(type: "TEXT", nullable: true),
                    buyer_address = table.Column<string>(type: "TEXT", nullable: true),
                    invoice_number = table.Column<string>(type: "TEXT", nullable: true),
                    invoice_issued_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    payment_method = table.Column<string>(type: "TEXT", nullable: true),
                    cheque_number = table.Column<string>(type: "TEXT", nullable: true),
                    cheque_bank_name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale", x => x.Id);
                    table.CheckConstraint("ck_sale_checkout_fee_cents", "checkout_fee_cents >= 0");
                    table.CheckConstraint("ck_sale_subtotal_seller_price_cents", "subtotal_seller_price_cents >= 0");
                    table.CheckConstraint("ck_sale_total_paid_cents", "total_paid_cents >= 0");
                    table.ForeignKey(
                        name: "FK_sale_cash_register_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_register",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    seller_id = table.Column<long>(type: "INTEGER", nullable: false),
                    reference = table.Column<string>(type: "TEXT", nullable: true),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    seller_price_cents = table.Column<int>(type: "INTEGER", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item", x => x.Id);
                    table.CheckConstraint("ck_item_seller_price_cents", "seller_price_cents IS NULL OR seller_price_cents >= 0");
                    table.CheckConstraint("ck_item_status", "status IN ('registered', 'in_stock', 'sold', 'missing', 'defective')");
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
                    corrected_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    refunded_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    refund_item_price_cents = table.Column<int>(type: "INTEGER", nullable: true),
                    refund_checkout_fee_cents = table.Column<int>(type: "INTEGER", nullable: true),
                    refund_total_cents = table.Column<int>(type: "INTEGER", nullable: true),
                    refund_reason = table.Column<string>(type: "TEXT", nullable: true),
                    refund_cash_register_id = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_line", x => x.Id);
                    table.CheckConstraint("ck_sale_line_refund_checkout_fee_cents", "refund_checkout_fee_cents IS NULL OR refund_checkout_fee_cents >= 0");
                    table.CheckConstraint("ck_sale_line_refund_item_price_cents", "refund_item_price_cents IS NULL OR refund_item_price_cents >= 0");
                    table.CheckConstraint("ck_sale_line_refund_total_cents", "refund_total_cents IS NULL OR refund_total_cents >= 0");
                    table.CheckConstraint("ck_sale_line_seller_price_cents", "seller_price_at_sale_cents >= 0");
                    table.ForeignKey(
                        name: "FK_sale_line_cash_register_refund_cash_register_id",
                        column: x => x.refund_cash_register_id,
                        principalTable: "cash_register",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.InsertData(
                table: "event_state",
                columns: new[] { "Id", "sale_phase", "updated_at" },
                values: new object[] { 1, "presale", new DateTime(2026, 5, 4, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_cash_register_name",
                table: "cash_register",
                column: "name",
                unique: true);

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
                name: "IX_item_status",
                table: "item",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_item_title",
                table: "item",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "IX_sale_cash_register_id",
                table: "sale",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_invoice_number",
                table: "sale",
                column: "invoice_number",
                unique: true,
                filter: "invoice_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sale_sold_at",
                table: "sale",
                column: "sold_at");

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_item_id",
                table: "sale_line",
                column: "item_id",
                unique: true,
                filter: "corrected_at IS NULL AND refunded_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_refund_cash_register_id",
                table: "sale_line",
                column: "refund_cash_register_id");

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
                name: "event_state");

            migrationBuilder.DropTable(
                name: "sale_line");

            migrationBuilder.DropTable(
                name: "item");

            migrationBuilder.DropTable(
                name: "sale");

            migrationBuilder.DropTable(
                name: "seller");

            migrationBuilder.DropTable(
                name: "cash_register");
        }
    }
}
