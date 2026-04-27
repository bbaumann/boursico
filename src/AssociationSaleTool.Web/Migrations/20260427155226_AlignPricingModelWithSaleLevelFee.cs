using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260427155226_AlignPricingModelWithSaleLevelFee")]
    public partial class AlignPricingModelWithSaleLevelFee : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE sale_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    sold_at TEXT NOT NULL,
                    subtotal_seller_price_cents INTEGER NOT NULL,
                    checkout_fee_cents INTEGER NOT NULL,
                    total_paid_cents INTEGER NOT NULL,
                    receipt_details TEXT,
                    CONSTRAINT ck_sale_subtotal_seller_price_cents
                        CHECK (subtotal_seller_price_cents >= 0),
                    CONSTRAINT ck_sale_checkout_fee_cents
                        CHECK (checkout_fee_cents >= 0),
                    CONSTRAINT ck_sale_total_paid_cents
                        CHECK (total_paid_cents >= 0)
                );

                INSERT INTO sale_new (Id, sold_at, subtotal_seller_price_cents, checkout_fee_cents, total_paid_cents, receipt_details)
                SELECT
                    s.Id,
                    s.sold_at,
                    COALESCE((SELECT SUM(sl.seller_price_at_sale_cents) FROM sale_line sl WHERE sl.sale_id = s.Id), 0),
                    (COALESCE((SELECT SUM(sl.seller_price_at_sale_cents) FROM sale_line sl WHERE sl.sale_id = s.Id), 0) + 5) / 10,
                    COALESCE((SELECT SUM(sl.seller_price_at_sale_cents) FROM sale_line sl WHERE sl.sale_id = s.Id), 0)
                        + ((COALESCE((SELECT SUM(sl.seller_price_at_sale_cents) FROM sale_line sl WHERE sl.sale_id = s.Id), 0) + 5) / 10),
                    s.receipt_details
                FROM sale s;

                CREATE TABLE item_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    seller_id INTEGER NOT NULL,
                    reference TEXT NOT NULL,
                    item_number INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    seller_price_cents INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    CONSTRAINT ck_item_status
                        CHECK (status IN ('registered', 'in_stock', 'not_provided', 'sold', 'missing')),
                    CONSTRAINT ck_item_seller_price_cents
                        CHECK (seller_price_cents >= 0),
                    CONSTRAINT FK_item_seller_seller_id
                        FOREIGN KEY (seller_id)
                        REFERENCES seller(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT uq_item_reference UNIQUE (reference),
                    CONSTRAINT uq_item_seller_number UNIQUE (seller_id, item_number)
                );

                INSERT INTO item_new (Id, seller_id, reference, item_number, title, seller_price_cents, status)
                SELECT Id, seller_id, reference, item_number, title, seller_price_cents, status
                FROM item;

                CREATE TABLE sale_line_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    sale_id INTEGER NOT NULL,
                    item_id INTEGER NOT NULL,
                    seller_price_at_sale_cents INTEGER NOT NULL,
                    CONSTRAINT ck_sale_line_seller_price_cents
                        CHECK (seller_price_at_sale_cents >= 0),
                    CONSTRAINT FK_sale_line_item_item_id
                        FOREIGN KEY (item_id)
                        REFERENCES item_new(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT FK_sale_line_sale_sale_id
                        FOREIGN KEY (sale_id)
                        REFERENCES sale_new(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT uq_sale_line_item UNIQUE (item_id)
                );

                INSERT INTO sale_line_new (Id, sale_id, item_id, seller_price_at_sale_cents)
                SELECT Id, sale_id, item_id, seller_price_at_sale_cents
                FROM sale_line;

                DROP TABLE sale_line;
                DROP TABLE item;
                DROP TABLE sale;

                ALTER TABLE sale_new RENAME TO sale;
                ALTER TABLE item_new RENAME TO item;
                ALTER TABLE sale_line_new RENAME TO sale_line;

                CREATE UNIQUE INDEX IX_item_reference ON item (reference);
                CREATE INDEX IX_item_seller_id ON item (seller_id);
                CREATE UNIQUE INDEX IX_item_seller_id_item_number ON item (seller_id, item_number);
                CREATE INDEX IX_item_status ON item (status);
                CREATE INDEX IX_item_title ON item (title);
                CREATE INDEX IX_sale_sold_at ON sale (sold_at);
                CREATE UNIQUE INDEX IX_sale_line_item_id ON sale_line (item_id);
                CREATE INDEX IX_sale_line_sale_id ON sale_line (sale_id);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE sale_old (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    sold_at TEXT NOT NULL,
                    total_amount_cents INTEGER NOT NULL,
                    receipt_details TEXT,
                    CONSTRAINT ck_sale_total_amount_cents
                        CHECK (total_amount_cents >= 0)
                );

                INSERT INTO sale_old (Id, sold_at, total_amount_cents, receipt_details)
                SELECT Id, sold_at, total_paid_cents, receipt_details
                FROM sale;

                CREATE TABLE item_old (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    seller_id INTEGER NOT NULL,
                    reference TEXT NOT NULL,
                    item_number INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    seller_price_cents INTEGER NOT NULL,
                    sale_price_cents INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    CONSTRAINT ck_item_sale_price_cents
                        CHECK (sale_price_cents >= 0),
                    CONSTRAINT ck_item_seller_price_cents
                        CHECK (seller_price_cents >= 0),
                    CONSTRAINT ck_item_status
                        CHECK (status IN ('registered', 'in_stock', 'not_provided', 'sold', 'missing')),
                    CONSTRAINT FK_item_seller_seller_id
                        FOREIGN KEY (seller_id)
                        REFERENCES seller(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT uq_item_reference UNIQUE (reference),
                    CONSTRAINT uq_item_seller_number UNIQUE (seller_id, item_number)
                );

                INSERT INTO item_old (Id, seller_id, reference, item_number, title, seller_price_cents, sale_price_cents, status)
                SELECT
                    Id,
                    seller_id,
                    reference,
                    item_number,
                    title,
                    seller_price_cents,
                    seller_price_cents + (seller_price_cents / 5),
                    status
                FROM item;

                CREATE TABLE sale_line_old (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    sale_id INTEGER NOT NULL,
                    item_id INTEGER NOT NULL,
                    seller_price_at_sale_cents INTEGER NOT NULL,
                    sale_price_at_sale_cents INTEGER NOT NULL,
                    CONSTRAINT ck_sale_line_sale_price_cents
                        CHECK (sale_price_at_sale_cents >= 0),
                    CONSTRAINT ck_sale_line_seller_price_cents
                        CHECK (seller_price_at_sale_cents >= 0),
                    CONSTRAINT FK_sale_line_item_item_id
                        FOREIGN KEY (item_id)
                        REFERENCES item_old(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT FK_sale_line_sale_sale_id
                        FOREIGN KEY (sale_id)
                        REFERENCES sale_old(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT uq_sale_line_item UNIQUE (item_id)
                );

                INSERT INTO sale_line_old (Id, sale_id, item_id, seller_price_at_sale_cents, sale_price_at_sale_cents)
                SELECT
                    Id,
                    sale_id,
                    item_id,
                    seller_price_at_sale_cents,
                    seller_price_at_sale_cents + (seller_price_at_sale_cents / 5)
                FROM sale_line;

                DROP TABLE sale_line;
                DROP TABLE item;
                DROP TABLE sale;

                ALTER TABLE sale_old RENAME TO sale;
                ALTER TABLE item_old RENAME TO item;
                ALTER TABLE sale_line_old RENAME TO sale_line;

                CREATE UNIQUE INDEX IX_item_reference ON item (reference);
                CREATE INDEX IX_item_seller_id ON item (seller_id);
                CREATE UNIQUE INDEX IX_item_seller_id_item_number ON item (seller_id, item_number);
                CREATE INDEX IX_item_status ON item (status);
                CREATE INDEX IX_item_title ON item (title);
                CREATE INDEX IX_sale_sold_at ON sale (sold_at);
                CREATE UNIQUE INDEX IX_sale_line_item_id ON sale_line (item_id);
                CREATE INDEX IX_sale_line_sale_id ON sale_line (sale_id);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }
    }
}
