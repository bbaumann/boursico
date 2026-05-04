using System;
using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260504140000_AddSalePhasesAndCorrections")]
    public partial class AddSalePhasesAndCorrections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sale_line_item_id",
                table: "sale_line");

            migrationBuilder.AddColumn<DateTime>(
                name: "corrected_at",
                table: "sale_line",
                type: "TEXT",
                nullable: true);

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

            migrationBuilder.Sql(
                "INSERT INTO event_state (Id, sale_phase, updated_at) VALUES (1, 'presale', '2026-05-04T00:00:00Z');");

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_item_id",
                table: "sale_line",
                column: "item_id",
                unique: true,
                filter: "corrected_at IS NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sale_line_item_id",
                table: "sale_line");

            migrationBuilder.DropTable(
                name: "event_state");

            migrationBuilder.DropColumn(
                name: "corrected_at",
                table: "sale_line");

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_item_id",
                table: "sale_line",
                column: "item_id",
                unique: true);
        }
    }
}
