using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260510120000_AddSellerDepositStatus")]
    public partial class AddSellerDepositStatus : Migration
    {
        /// <inheritdoc />
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
                    postal_code TEXT NOT NULL,
                    city TEXT NOT NULL,
                    fee_paid_cents INTEGER NOT NULL DEFAULT 0,
                    deposit_status TEXT NOT NULL DEFAULT 'deposit_pending',
                    notes TEXT,
                    CONSTRAINT ck_seller_fee_paid_cents
                        CHECK (fee_paid_cents >= 0),
                    CONSTRAINT ck_seller_deposit_status
                        CHECK (deposit_status IN ('deposit_pending', 'deposit_validated'))
                );

                INSERT INTO seller_new (
                    Id,
                    seller_code,
                    first_name,
                    last_name,
                    email,
                    phone,
                    postal_address,
                    postal_code,
                    city,
                    fee_paid_cents,
                    deposit_status,
                    notes)
                SELECT
                    Id,
                    seller_code,
                    first_name,
                    last_name,
                    email,
                    phone,
                    postal_address,
                    postal_code,
                    city,
                    fee_paid_cents,
                    'deposit_pending',
                    notes
                FROM seller;

                DROP TABLE seller;

                ALTER TABLE seller_new RENAME TO seller;

                CREATE UNIQUE INDEX IX_seller_seller_code ON seller (seller_code);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
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
                    postal_code TEXT NOT NULL,
                    city TEXT NOT NULL,
                    fee_paid_cents INTEGER NOT NULL DEFAULT 0,
                    notes TEXT,
                    CONSTRAINT ck_seller_fee_paid_cents
                        CHECK (fee_paid_cents >= 0)
                );

                INSERT INTO seller_old (
                    Id,
                    seller_code,
                    first_name,
                    last_name,
                    email,
                    phone,
                    postal_address,
                    postal_code,
                    city,
                    fee_paid_cents,
                    notes)
                SELECT
                    Id,
                    seller_code,
                    first_name,
                    last_name,
                    email,
                    phone,
                    postal_address,
                    postal_code,
                    city,
                    fee_paid_cents,
                    notes
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
