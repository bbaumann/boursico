using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
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
}
