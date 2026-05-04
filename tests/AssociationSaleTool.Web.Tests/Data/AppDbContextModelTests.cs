using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Data;

public class AppDbContextModelTests
{
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
                && index.GetFilter() == "corrected_at IS NULL"
                && index.Properties.Select(property => property.Name).SequenceEqual([nameof(SaleLineEntity.ItemId)]));
    }

    [Fact]
    public void ItemStatusCheckConstraintExcludesNotProvided()
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
        Assert.Contains("CHECK (status IN ('registered', 'in_stock', 'sold', 'missing'))", createTableSql);
    }
}
