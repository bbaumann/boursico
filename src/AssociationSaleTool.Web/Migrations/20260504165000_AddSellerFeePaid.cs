using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260504165000_AddSellerFeePaid")]
    public partial class AddSellerFeePaid : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE seller_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    seller_code TEXT NOT NULL,
                    first_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    email TEXT,
                    phone TEXT NOT NULL,
                    postal_address TEXT NOT NULL,
                    fee_paid_cents INTEGER NOT NULL DEFAULT 0,
                    notes TEXT,
                    CONSTRAINT ck_seller_fee_paid_cents
                        CHECK (fee_paid_cents >= 0)
                );

                INSERT INTO seller_new (Id, seller_code, first_name, last_name, email, phone, postal_address, fee_paid_cents, notes)
                SELECT Id, seller_code, first_name, last_name, email, phone, postal_address, 0, notes
                FROM seller;

                DROP TABLE seller;

                ALTER TABLE seller_new RENAME TO seller;

                CREATE UNIQUE INDEX IX_seller_seller_code ON seller (seller_code);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE seller_old (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    seller_code TEXT NOT NULL,
                    first_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    email TEXT,
                    phone TEXT NOT NULL,
                    postal_address TEXT NOT NULL,
                    notes TEXT
                );

                INSERT INTO seller_old (Id, seller_code, first_name, last_name, email, phone, postal_address, notes)
                SELECT Id, seller_code, first_name, last_name, email, phone, postal_address, notes
                FROM seller;

                DROP TABLE seller;

                ALTER TABLE seller_old RENAME TO seller;

                CREATE UNIQUE INDEX IX_seller_seller_code ON seller (seller_code);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }
    }
}
