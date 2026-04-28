using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class CheckoutServiceTests
{
    [Fact]
    public async Task ListCatalogItemsAsync_ReturnsOnlyInStockItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var seller = new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.test"
        };

        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var inStockItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-001",
            ItemNumber = 1,
            Title = "Train en bois",
            SellerPriceCents = 500,
            Status = "in_stock"
        };
        var registeredItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-002",
            ItemNumber = 2,
            Title = "Puzzle",
            SellerPriceCents = 650,
            Status = "registered"
        };

        context.Items.AddRange(inStockItem, registeredItem);
        await context.SaveChangesAsync();

        var service = new CheckoutService(context);

        var catalog = await service.ListCatalogItemsAsync();

        var item = Assert.Single(catalog);
        Assert.Equal(inStockItem.Id, item.Id);
        Assert.Equal("AA-001", item.Reference);
        Assert.Equal("Train en bois", item.Title);
        Assert.Equal(500, item.SellerPriceCents);
    }
}
