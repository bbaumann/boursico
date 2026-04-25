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
}
