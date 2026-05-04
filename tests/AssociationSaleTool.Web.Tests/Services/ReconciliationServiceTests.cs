using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
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
        var emptySeller = new SellerEntity { SellerCode = "FF", FirstName = "Mary", LastName = "Jackson" };

        context.Sellers.AddRange(soldSeller, missingSeller, inStockSeller, registeredSeller, emptySeller);
        await context.SaveChangesAsync();

        var soldItem = AddItem(context, soldSeller, "AA-001", 1, "Train", 400, "sold");
        AddItem(context, missingSeller, "BB-001", 1, "Puzzle", 500, "missing");
        AddItem(context, inStockSeller, "CC-001", 1, "Camion", 600, "in_stock");
        AddItem(context, registeredSeller, "DD-001", 1, "Piano", 700, "registered");
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

    [Fact]
    public async Task ListSellerSheetsAsync_FiltersByTrimmedUppercaseSellerCode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var firstSeller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        var secondSeller = new SellerEntity { SellerCode = "BB", FirstName = "Grace", LastName = "Hopper" };
        context.Sellers.AddRange(firstSeller, secondSeller);
        await context.SaveChangesAsync();

        AddItem(context, firstSeller, "AA-001", 1, "Train", 500, "in_stock");
        AddItem(context, secondSeller, "BB-001", 1, "Puzzle", 600, "in_stock");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var sheet = Assert.Single(await service.ListSellerSheetsAsync(" aa "));

        Assert.Equal("AA", sheet.SellerCode);
    }

    [Fact]
    public async Task ListSellerSheetsAsync_TreatsWhitespaceSellerCodeAsNoFilter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var firstSeller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        var secondSeller = new SellerEntity { SellerCode = "BB", FirstName = "Grace", LastName = "Hopper" };
        context.Sellers.AddRange(firstSeller, secondSeller);
        await context.SaveChangesAsync();

        AddItem(context, firstSeller, "AA-001", 1, "Train", 500, "in_stock");
        AddItem(context, secondSeller, "BB-001", 1, "Puzzle", 600, "in_stock");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var sheets = await service.ListSellerSheetsAsync("   ");

        Assert.Equal(["AA", "BB"], sheets.Select(sheet => sheet.SellerCode));
    }

    [Fact]
    public async Task ListSellerSheetOptionsAsync_ReturnsActiveSellersOrderedWithNames()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var secondSeller = new SellerEntity { SellerCode = "BB", FirstName = "Grace", LastName = "Hopper" };
        var firstSeller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        var inactiveSeller = new SellerEntity { SellerCode = "CC", FirstName = "Mary", LastName = "Jackson" };
        context.Sellers.AddRange(secondSeller, firstSeller, inactiveSeller);
        await context.SaveChangesAsync();

        AddItem(context, secondSeller, "BB-001", 1, "Puzzle", 600, "missing");
        AddItem(context, firstSeller, "AA-001", 1, "Train", 500, "in_stock");
        AddItem(context, inactiveSeller, "CC-001", 1, "Livre", 400, "registered");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var options = await service.ListSellerSheetOptionsAsync();

        Assert.Equal(["AA", "BB"], options.Select(option => option.SellerCode));
        Assert.Equal(["Ada Lovelace", "Grace Hopper"], options.Select(option => option.SellerName));
    }

    [Fact]
    public async Task ListCorrectionItemsAsync_ReturnsOnlyInStockAndMissingItemsOrderedByReference()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var missing = AddItem(context, seller, "AA-002", 2, "Puzzle", 700, "missing");
        var inStock = AddItem(context, seller, "AA-001", 1, "Train", 500, "in_stock");
        AddItem(context, seller, "AA-003", 3, "Livre", 400, "registered");
        AddItem(context, seller, "AA-004", 4, "Piano", 800, "sold");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var items = await service.ListCorrectionItemsAsync();

        Assert.Equal([inStock.Id, missing.Id], items.Select(item => item.ItemId));
        Assert.Equal(["AA-001", "AA-002"], items.Select(item => item.Reference));
        Assert.All(items, item => Assert.Equal("AA", item.SellerCode));
    }

    [Fact]
    public async Task SearchItemsAsync_FindsItemsByPartialReference()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var expectedItem = AddItem(context, seller, "AA-001", 1, "Train", 500, "in_stock");
        AddItem(context, seller, "AA-002", 2, "Puzzle", 600, "in_stock");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var result = Assert.Single(await service.SearchItemsAsync(" 001 "));

        Assert.Equal(expectedItem.Id, result.ItemId);
        Assert.Equal("AA-001", result.Reference);
    }

    [Fact]
    public async Task SearchItemsAsync_FindsItemsByPartialTitleCaseInsensitively()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        AddItem(context, seller, "AA-001", 1, "Train", 500, "in_stock");
        var expectedItem = AddItem(context, seller, "AA-002", 2, "Puzzle en bois", 600, "in_stock");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var result = Assert.Single(await service.SearchItemsAsync("puzzle"));

        Assert.Equal(expectedItem.Id, result.ItemId);
        Assert.Equal("Puzzle en bois", result.Title);
    }

    [Fact]
    public async Task SearchItemsAsync_FindsItemsByAccentedTitleCaseInsensitively()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        AddItem(context, seller, "AA-001", 1, "Train", 500, "in_stock");
        var expectedItem = AddItem(context, seller, "AA-002", 2, "ÉCOLE en bois", 600, "in_stock");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var result = Assert.Single(await service.SearchItemsAsync("école"));

        Assert.Equal(expectedItem.Id, result.ItemId);
        Assert.Equal("ÉCOLE en bois", result.Title);
    }

    [Fact]
    public async Task SearchItemsAsync_ReturnsSellerContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var item = AddItem(context, seller, "AA-001", 1, "Train", 500, "in_stock");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var result = Assert.Single(await service.SearchItemsAsync("Train"));

        Assert.Equal(item.Id, result.ItemId);
        Assert.Equal(seller.Id, result.SellerId);
        Assert.Equal("AA", result.SellerCode);
        Assert.Equal("Ada Lovelace", result.SellerName);
    }

    [Fact]
    public async Task SearchItemsAsync_ReturnsEmptyListForBlankQuery()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        AddItem(context, seller, "AA-001", 1, "Train", 500, "in_stock");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var results = await service.SearchItemsAsync("   ");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchItemsAsync_OrdersByReferenceAndCapsResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        for (var itemNumber = 30; itemNumber >= 1; itemNumber--)
        {
            AddItem(
                context,
                seller,
                $"AA-{itemNumber:000}",
                itemNumber,
                "Lot commun",
                500,
                "in_stock");
        }

        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var results = await service.SearchItemsAsync("Lot");

        Assert.Equal(25, results.Count);
        Assert.Equal(
            Enumerable.Range(1, 25).Select(itemNumber => $"AA-{itemNumber:000}"),
            results.Select(result => result.Reference));
    }

    [Fact]
    public async Task MarkItemMissingAsync_ChangesInStockItemToMissing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var item = AddItem(context, seller, "AA-001", 1, "Train", 500, "in_stock");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var result = await service.MarkItemMissingAsync(item.Id);

        Assert.Equal(ReconciliationStatusChangeResult.Changed, result);
        Assert.Equal("missing", await GetSavedItemStatusAsync(connection));
    }

    [Fact]
    public async Task MarkItemInStockAsync_ChangesMissingItemToInStock()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var item = AddItem(context, seller, "AA-001", 1, "Train", 500, "missing");
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var result = await service.MarkItemInStockAsync(item.Id);

        Assert.Equal(ReconciliationStatusChangeResult.Changed, result);
        Assert.Equal("in_stock", await GetSavedItemStatusAsync(connection));
    }

    [Fact]
    public async Task MarkItemMissingAsync_ReturnsNotAllowedWhenTrackedItemWasChangedByAnotherContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var options = await CreateOptionsWithSchemaAsync(connection);

        await using var firstContext = new AppDbContext(options);
        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        firstContext.Sellers.Add(seller);
        await firstContext.SaveChangesAsync();

        var item = AddItem(firstContext, seller, "AA-001", 1, "Train", 500, "in_stock");
        await firstContext.SaveChangesAsync();

        await using var secondContext = new AppDbContext(options);
        var sameItem = await secondContext.Items.SingleAsync(x => x.Id == item.Id);
        sameItem.Status = "sold";
        await secondContext.SaveChangesAsync();

        var service = new ReconciliationService(firstContext);

        var result = await service.MarkItemMissingAsync(item.Id);

        Assert.Equal(ReconciliationStatusChangeResult.NotAllowed, result);
        Assert.Equal("sold", await GetSavedItemStatusAsync(connection));
    }

    [Fact]
    public async Task MarkItemMissingAsync_ReturnsNotFoundWhenItemDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var service = new ReconciliationService(context);

        var result = await service.MarkItemMissingAsync(999);

        Assert.Equal(ReconciliationStatusChangeResult.NotFound, result);
    }

    [Fact]
    public async Task MarkItemInStockAsync_ReturnsNotFoundWhenItemDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var service = new ReconciliationService(context);

        var result = await service.MarkItemInStockAsync(999);

        Assert.Equal(ReconciliationStatusChangeResult.NotFound, result);
    }

    [Theory]
    [InlineData("registered")]
    [InlineData("sold")]
    [InlineData("missing")]
    public async Task MarkItemMissingAsync_RejectsItemsThatAreNotInStock(string status)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var item = AddItem(context, seller, "AA-001", 1, "Train", 500, status);
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var result = await service.MarkItemMissingAsync(item.Id);

        Assert.Equal(ReconciliationStatusChangeResult.NotAllowed, result);
        Assert.Equal(status, await GetSavedItemStatusAsync(connection));
    }

    [Theory]
    [InlineData("registered")]
    [InlineData("sold")]
    [InlineData("in_stock")]
    public async Task MarkItemInStockAsync_RejectsItemsThatAreNotMissing(string status)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var item = AddItem(context, seller, "AA-001", 1, "Train", 500, status);
        await context.SaveChangesAsync();

        var service = new ReconciliationService(context);

        var result = await service.MarkItemInStockAsync(item.Id);

        Assert.Equal(ReconciliationStatusChangeResult.NotAllowed, result);
        Assert.Equal(status, await GetSavedItemStatusAsync(connection));
    }

    private static async Task<AppDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = await CreateOptionsWithSchemaAsync(connection);

        return new AppDbContext(options);
    }

    private static async Task<DbContextOptions<AppDbContext>> CreateOptionsWithSchemaAsync(SqliteConnection connection)
    {
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return options;
    }

    private static async Task<string> GetSavedItemStatusAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        return await context.Items
            .AsNoTracking()
            .Select(x => x.Status)
            .SingleAsync();
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
