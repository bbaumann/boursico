using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class ReconciliationServiceTests
{
    [Fact]
    public async Task ListSellerSheetsAsync_UsesSoldSnapshotAndMissingCurrentPrice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var sold = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-001",
            ItemNumber = 1,
            Title = "Train",
            SellerPriceCents = 9999,
            Status = "sold"
        };
        var missing = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-002",
            ItemNumber = 2,
            Title = "Puzzle",
            SellerPriceCents = 700,
            Status = "missing"
        };

        context.Items.AddRange(sold, missing);
        await context.SaveChangesAsync();

        context.Sales.Add(new SaleEntity
        {
            SoldAt = DateTime.UtcNow,
            SubtotalSellerPriceCents = 500,
            CheckoutFeeCents = 50,
            TotalPaidCents = 550,
            SaleLines = [new SaleLineEntity { ItemId = sold.Id, SellerPriceAtSaleCents = 500 }]
        });
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var sheet = Assert.Single(await service.ListSellerSheetsAsync());

        Assert.Equal(1200, sheet.TotalPayableCents);
        Assert.Collection(
            sheet.PayableItems,
            soldItem =>
            {
                Assert.Equal(sold.Id, soldItem.ItemId);
                Assert.Equal("sold", soldItem.Status);
                Assert.Equal(500, soldItem.PriceCents);
                Assert.False(soldItem.IsMissing);
            },
            missingItem =>
            {
                Assert.Equal(missing.Id, missingItem.ItemId);
                Assert.Equal("missing", missingItem.Status);
                Assert.Equal(700, missingItem.PriceCents);
                Assert.True(missingItem.IsMissing);
            });
    }

    [Fact]
    public async Task ListSellerSheetsAsync_IncludesOnlySellersWithActiveReconciliationItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var soldSeller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        var missingSeller = new SellerEntity { SellerCode = "BB", FirstName = "Grace", LastName = "Hopper" };
        var inStockSeller = new SellerEntity { SellerCode = "CC", FirstName = "Katherine", LastName = "Johnson" };
        var registeredSeller = new SellerEntity { SellerCode = "DD", FirstName = "Margaret", LastName = "Hamilton" };
        var notProvidedSeller = new SellerEntity { SellerCode = "EE", FirstName = "Annie", LastName = "Easley" };
        var emptySeller = new SellerEntity { SellerCode = "FF", FirstName = "Mary", LastName = "Jackson" };

        context.Sellers.AddRange(soldSeller, missingSeller, inStockSeller, registeredSeller, notProvidedSeller, emptySeller);
        await context.SaveChangesAsync();

        var soldItem = AddItem(context, soldSeller, "AA-001", 1, "Train", 400, "sold");
        AddItem(context, missingSeller, "BB-001", 1, "Puzzle", 500, "missing");
        AddItem(context, inStockSeller, "CC-001", 1, "Camion", 600, "in_stock");
        AddItem(context, registeredSeller, "DD-001", 1, "Piano", 700, "registered");
        AddItem(context, notProvidedSeller, "EE-001", 1, "Livre", 800, "not_provided");
        await context.SaveChangesAsync();

        context.Sales.Add(new SaleEntity
        {
            SoldAt = DateTime.UtcNow,
            SubtotalSellerPriceCents = 400,
            CheckoutFeeCents = 40,
            TotalPaidCents = 440,
            SaleLines = [new SaleLineEntity { ItemId = soldItem.Id, SellerPriceAtSaleCents = 400 }]
        });
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var sheets = await service.ListSellerSheetsAsync();

        Assert.Equal(["AA", "BB", "CC"], sheets.Select(sheet => sheet.SellerCode));
    }

    [Fact]
    public async Task ListSellerSheetsAsync_ExcludesInactiveItemsAndReturnsOnlyInStockItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var sold = AddItem(context, seller, "AA-003", 3, "Train", 900, "sold");
        var missing = AddItem(context, seller, "AA-002", 2, "Puzzle", 700, "missing");
        var inStock = AddItem(context, seller, "AA-001", 1, "Camion", 600, "in_stock");
        AddItem(context, seller, "AA-004", 4, "Piano", 500, "registered");
        AddItem(context, seller, "AA-005", 5, "Livre", 400, "not_provided");
        await context.SaveChangesAsync();

        context.Sales.Add(new SaleEntity
        {
            SoldAt = DateTime.UtcNow,
            SubtotalSellerPriceCents = 900,
            CheckoutFeeCents = 90,
            TotalPaidCents = 990,
            SaleLines = [new SaleLineEntity { ItemId = sold.Id, SellerPriceAtSaleCents = 900 }]
        });
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var sheet = Assert.Single(await service.ListSellerSheetsAsync());

        Assert.Equal([missing.Id, sold.Id], sheet.PayableItems.Select(item => item.ItemId));
        Assert.Equal([inStock.Id], sheet.ReturnedItems.Select(item => item.ItemId));
        Assert.All(sheet.ReturnedItems, item => Assert.Equal("in_stock", item.Status));
    }

    private static ItemEntity AddItem(
        AppDbContext context,
        SellerEntity seller,
        string reference,
        int itemNumber,
        string title,
        int sellerPriceCents,
        string status)
    {
        var item = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = reference,
            ItemNumber = itemNumber,
            Title = title,
            SellerPriceCents = sellerPriceCents,
            Status = status
        };

        context.Items.Add(item);
        return item;
    }
}
