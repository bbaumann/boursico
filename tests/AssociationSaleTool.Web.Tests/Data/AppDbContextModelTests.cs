using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Data;

public class AppDbContextModelTests
{
    [Fact]
    public void SellerStoresRequiredContactColumns()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(SellerEntity));
        Assert.NotNull(entityType);
        var phone = Assert.Single(entityType!.GetProperties(), property => property.GetColumnName() == "phone");
        var postalAddress = Assert.Single(entityType.GetProperties(), property => property.GetColumnName() == "postal_address");
        var postalCode = Assert.Single(entityType.GetProperties(), property => property.GetColumnName() == "postal_code");
        var city = Assert.Single(entityType.GetProperties(), property => property.GetColumnName() == "city");
        Assert.False(phone.IsNullable);
        Assert.False(postalAddress.IsNullable);
        Assert.False(postalCode.IsNullable);
        Assert.False(city.IsNullable);
    }

    [Fact]
    public void SellerStoresFeePaidColumn()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(SellerEntity));
        Assert.NotNull(entityType);
        var feePaid = Assert.Single(entityType!.GetProperties(), property => property.Name == nameof(SellerEntity.FeePaidCents));
        Assert.Equal("fee_paid_cents", feePaid.GetColumnName());
        Assert.False(feePaid.IsNullable);
    }

    [Fact]
    public void SellerStoresDepositStatusColumn()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(SellerEntity));
        Assert.NotNull(entityType);
        var depositStatus = Assert.Single(entityType!.GetProperties(), property => property.Name == nameof(SellerEntity.DepositStatus));
        Assert.Equal("deposit_status", depositStatus.GetColumnName());
        Assert.False(depositStatus.IsNullable);
        Assert.Equal("deposit_pending", depositStatus.GetDefaultValue());
    }

    [Fact]
    public void SellerStoresImportNotesColumn()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(SellerEntity));
        Assert.NotNull(entityType);
        var importNotes = Assert.Single(entityType!.GetProperties(), property => property.Name == nameof(SellerEntity.ImportNotes));
        Assert.Equal("import_notes", importNotes.GetColumnName());
        Assert.True(importNotes.IsNullable);
    }

    [Fact]
    public void SellerTableRequiresContactFields()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'seller'";
        var createTableSql = Assert.IsType<string>(command.ExecuteScalar());
        Assert.Contains("\"phone\" TEXT NOT NULL", createTableSql);
        Assert.Contains("\"postal_address\" TEXT NOT NULL", createTableSql);
        Assert.Contains("\"postal_code\" TEXT NOT NULL", createTableSql);
        Assert.Contains("\"city\" TEXT NOT NULL", createTableSql);
    }

    [Fact]
    public void SellerTableRequiresNonNegativeFeePaid()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'seller'";
        var createTableSql = Assert.IsType<string>(command.ExecuteScalar());
        Assert.Contains("\"fee_paid_cents\" INTEGER NOT NULL DEFAULT 0", createTableSql);
        Assert.Contains("CHECK (fee_paid_cents >= 0)", createTableSql);
    }

    [Fact]
    public void SellerTableRestrictsDepositStatusValues()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'seller'";
        var createTableSql = Assert.IsType<string>(command.ExecuteScalar());
        Assert.Contains("\"deposit_status\" TEXT NOT NULL DEFAULT 'deposit_pending'", createTableSql);
        Assert.Contains("CHECK (deposit_status IN ('deposit_pending', 'deposit_validated'))", createTableSql);
    }

    [Fact]
    public void ItemReferenceHasUniqueIndex()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(ItemEntity));
        Assert.NotNull(entityType);
        Assert.Contains(
            entityType!.GetIndexes(),
            index => index.IsUnique && index.Properties.Any(property => property.Name == nameof(ItemEntity.Reference)));
    }

    [Fact]
    public void ItemHasCompositeUniqueIndexForSellerAndNumber()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(ItemEntity));
        Assert.NotNull(entityType);
        Assert.Contains(
            entityType!.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Select(property => property.Name).SequenceEqual(
                    [nameof(ItemEntity.SellerId), nameof(ItemEntity.ItemNumber)]));
    }

    [Fact]
    public void ItemDoesNotStoreDerivedSalePriceColumn()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(ItemEntity));
        Assert.NotNull(entityType);
        Assert.DoesNotContain(
            entityType!.GetProperties(),
            property => property.GetColumnName() == "sale_price_cents");
    }

    [Fact]
    public void SaleStoresSubtotalFeeAndTotalColumns()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var saleEntityType = context.Model.FindEntityType(typeof(SaleEntity));
        Assert.NotNull(saleEntityType);
        Assert.Contains(saleEntityType!.GetProperties(), property => property.GetColumnName() == "subtotal_seller_price_cents");
        Assert.Contains(saleEntityType.GetProperties(), property => property.GetColumnName() == "checkout_fee_cents");
        Assert.Contains(saleEntityType.GetProperties(), property => property.GetColumnName() == "total_paid_cents");
    }

    [Fact]
    public void SaleStoresPaymentTraceabilityColumns()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var saleEntityType = context.Model.FindEntityType(typeof(SaleEntity));
        Assert.NotNull(saleEntityType);
        Assert.Contains(saleEntityType!.GetProperties(), property => property.GetColumnName() == "payment_method");
        Assert.Contains(saleEntityType.GetProperties(), property => property.GetColumnName() == "cheque_number");
        Assert.Contains(saleEntityType.GetProperties(), property => property.GetColumnName() == "cheque_bank_name");
    }

    [Fact]
    public void SaleStoresBuyerIdentityColumns()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var saleEntityType = context.Model.FindEntityType(typeof(SaleEntity));
        Assert.NotNull(saleEntityType);
        Assert.Contains(saleEntityType!.GetProperties(), property => property.GetColumnName() == "buyer_name");
        Assert.Contains(saleEntityType.GetProperties(), property => property.GetColumnName() == "buyer_postal_code");
        Assert.Contains(saleEntityType.GetProperties(), property => property.GetColumnName() == "buyer_city");
        Assert.Contains(saleEntityType.GetProperties(), property => property.GetColumnName() == "buyer_address");
        Assert.DoesNotContain(saleEntityType.GetProperties(), property => property.GetColumnName() == "receipt_details");
    }

    [Fact]
    public void SaleStoresInvoiceColumnsWithUniqueInvoiceNumber()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var saleEntityType = context.Model.FindEntityType(typeof(SaleEntity));
        Assert.NotNull(saleEntityType);
        Assert.Contains(saleEntityType!.GetProperties(), property => property.GetColumnName() == "invoice_number");
        Assert.Contains(saleEntityType.GetProperties(), property => property.GetColumnName() == "invoice_issued_at");
        Assert.Contains(
            saleEntityType.GetIndexes(),
            index => index.IsUnique
                && index.GetFilter() == "invoice_number IS NOT NULL"
                && index.Properties.Select(property => property.Name).SequenceEqual([nameof(SaleEntity.InvoiceNumber)]));
    }

    [Fact]
    public void EventStateStoresSalePhase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var eventStateEntityType = context.Model.FindEntityType(typeof(EventStateEntity));
        Assert.NotNull(eventStateEntityType);
        Assert.Contains(eventStateEntityType!.GetProperties(), property => property.GetColumnName() == "sale_phase");
    }

    [Fact]
    public void SaleLineStoresCorrectionTimestamp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var saleLineEntityType = context.Model.FindEntityType(typeof(SaleLineEntity));
        Assert.NotNull(saleLineEntityType);
        Assert.Contains(saleLineEntityType!.GetProperties(), property => property.GetColumnName() == "corrected_at");
    }

    [Fact]
    public void SaleLineStoresRefundFields()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var saleLineEntityType = context.Model.FindEntityType(typeof(SaleLineEntity));
        Assert.NotNull(saleLineEntityType);
        Assert.Contains(saleLineEntityType!.GetProperties(), property => property.GetColumnName() == "refunded_at");
        Assert.Contains(saleLineEntityType.GetProperties(), property => property.GetColumnName() == "refund_item_price_cents");
        Assert.Contains(saleLineEntityType.GetProperties(), property => property.GetColumnName() == "refund_checkout_fee_cents");
        Assert.Contains(saleLineEntityType.GetProperties(), property => property.GetColumnName() == "refund_total_cents");
        Assert.Contains(saleLineEntityType.GetProperties(), property => property.GetColumnName() == "refund_reason");
    }

    [Fact]
    public void SaleLineAllowsOnlyOneActiveSaleLinePerItem()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var saleLineEntityType = context.Model.FindEntityType(typeof(SaleLineEntity));
        Assert.NotNull(saleLineEntityType);
        Assert.Contains(
            saleLineEntityType!.GetIndexes(),
            index => index.IsUnique
                && index.GetFilter() == "corrected_at IS NULL AND refunded_at IS NULL"
                && index.Properties.Select(property => property.Name).SequenceEqual([nameof(SaleLineEntity.ItemId)]));
    }

    [Fact]
    public void ItemStatusCheckConstraintAllowsDefectiveAndExcludesNotProvided()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'item'";
        var createTableSql = Assert.IsType<string>(command.ExecuteScalar());
        Assert.DoesNotContain("not_provided", createTableSql);
        Assert.Contains("CHECK (status IN ('registered', 'in_stock', 'sold', 'missing', 'defective'))", createTableSql);
    }

    [Fact]
    public void SaleLineRefundAmountsAreNonNegativeWhenPresent()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'sale_line'";
        var createTableSql = Assert.IsType<string>(command.ExecuteScalar());
        Assert.Contains("CHECK (refund_item_price_cents IS NULL OR refund_item_price_cents >= 0)", createTableSql);
        Assert.Contains("CHECK (refund_checkout_fee_cents IS NULL OR refund_checkout_fee_cents >= 0)", createTableSql);
        Assert.Contains("CHECK (refund_total_cents IS NULL OR refund_total_cents >= 0)", createTableSql);
    }
}
