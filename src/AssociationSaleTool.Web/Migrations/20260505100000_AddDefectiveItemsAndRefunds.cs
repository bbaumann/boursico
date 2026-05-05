using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDefectiveItemsAndRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sale_line_item_id",
                table: "sale_line");

            migrationBuilder.DropCheckConstraint(
                name: "ck_item_status",
                table: "item");

            migrationBuilder.AddColumn<int>(
                name: "refund_checkout_fee_cents",
                table: "sale_line",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "refund_item_price_cents",
                table: "sale_line",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refund_reason",
                table: "sale_line",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "refund_total_cents",
                table: "sale_line",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "refunded_at",
                table: "sale_line",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_item_id",
                table: "sale_line",
                column: "item_id",
                unique: true,
                filter: "corrected_at IS NULL AND refunded_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sale_line_refund_checkout_fee_cents",
                table: "sale_line",
                sql: "refund_checkout_fee_cents IS NULL OR refund_checkout_fee_cents >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sale_line_refund_item_price_cents",
                table: "sale_line",
                sql: "refund_item_price_cents IS NULL OR refund_item_price_cents >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sale_line_refund_total_cents",
                table: "sale_line",
                sql: "refund_total_cents IS NULL OR refund_total_cents >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_item_status",
                table: "item",
                sql: "status IN ('registered', 'in_stock', 'sold', 'missing', 'defective')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sale_line_item_id",
                table: "sale_line");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sale_line_refund_checkout_fee_cents",
                table: "sale_line");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sale_line_refund_item_price_cents",
                table: "sale_line");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sale_line_refund_total_cents",
                table: "sale_line");

            migrationBuilder.DropCheckConstraint(
                name: "ck_item_status",
                table: "item");

            migrationBuilder.DropColumn(
                name: "refund_checkout_fee_cents",
                table: "sale_line");

            migrationBuilder.DropColumn(
                name: "refund_item_price_cents",
                table: "sale_line");

            migrationBuilder.DropColumn(
                name: "refund_reason",
                table: "sale_line");

            migrationBuilder.DropColumn(
                name: "refund_total_cents",
                table: "sale_line");

            migrationBuilder.DropColumn(
                name: "refunded_at",
                table: "sale_line");

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_item_id",
                table: "sale_line",
                column: "item_id",
                unique: true,
                filter: "corrected_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_item_status",
                table: "item",
                sql: "status IN ('registered', 'in_stock', 'sold', 'missing')");
        }
    }
}
