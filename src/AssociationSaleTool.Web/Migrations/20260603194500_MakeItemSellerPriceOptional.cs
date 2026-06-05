using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260603194500_MakeItemSellerPriceOptional")]
    public partial class MakeItemSellerPriceOptional : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE item_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    seller_id INTEGER NOT NULL,
                    reference TEXT NOT NULL,
                    item_number INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    seller_price_cents INTEGER,
                    status TEXT NOT NULL,
                    CONSTRAINT ck_item_status
                        CHECK (status IN ('registered', 'in_stock', 'sold', 'missing', 'defective')),
                    CONSTRAINT ck_item_seller_price_cents
                        CHECK (seller_price_cents IS NULL OR seller_price_cents >= 0),
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

                DROP TABLE item;

                ALTER TABLE item_new RENAME TO item;

                CREATE UNIQUE INDEX IX_item_reference ON item (reference);
                CREATE INDEX IX_item_seller_id ON item (seller_id);
                CREATE UNIQUE INDEX IX_item_seller_id_item_number ON item (seller_id, item_number);
                CREATE INDEX IX_item_status ON item (status);
                CREATE INDEX IX_item_title ON item (title);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE item_old (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    seller_id INTEGER NOT NULL,
                    reference TEXT NOT NULL,
                    item_number INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    seller_price_cents INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    CONSTRAINT ck_item_status
                        CHECK (status IN ('registered', 'in_stock', 'sold', 'missing', 'defective')),
                    CONSTRAINT ck_item_seller_price_cents
                        CHECK (seller_price_cents >= 0),
                    CONSTRAINT FK_item_seller_seller_id
                        FOREIGN KEY (seller_id)
                        REFERENCES seller(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT uq_item_reference UNIQUE (reference),
                    CONSTRAINT uq_item_seller_number UNIQUE (seller_id, item_number)
                );

                INSERT INTO item_old (Id, seller_id, reference, item_number, title, seller_price_cents, status)
                SELECT Id, seller_id, reference, item_number, title, COALESCE(seller_price_cents, 0), status
                FROM item;

                DROP TABLE item;

                ALTER TABLE item_old RENAME TO item;

                CREATE UNIQUE INDEX IX_item_reference ON item (reference);
                CREATE INDEX IX_item_seller_id ON item (seller_id);
                CREATE UNIQUE INDEX IX_item_seller_id_item_number ON item (seller_id, item_number);
                CREATE INDEX IX_item_status ON item (status);
                CREATE INDEX IX_item_title ON item (title);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }
    }
}
