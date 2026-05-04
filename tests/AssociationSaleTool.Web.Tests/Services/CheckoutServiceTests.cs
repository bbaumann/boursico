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

    [Fact]
    public async Task ValidateCartAsync_ReturnsTotalsForInStockItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var (firstItem, secondItem) = await SeedTwoItemsAsync(context, "in_stock", "in_stock");
        var service = new CheckoutService(context);

        var result = await service.ValidateCartAsync([firstItem.Id, secondItem.Id]);

        Assert.True(result.IsValid);
        Assert.Empty(result.ConflictItemIds);
        Assert.Equal(2, result.ItemCount);
        Assert.NotNull(result.Totals);
        Assert.Equal(1500, result.Totals!.SubtotalSellerPriceCents);
        Assert.Equal(150, result.Totals.CheckoutFeeCents);
        Assert.Equal(1650, result.Totals.TotalPaidCents);
    }

    [Fact]
    public async Task ValidateCartAsync_ReturnsConflictsForItemsThatAreNotInStock()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var (firstItem, secondItem) = await SeedTwoItemsAsync(context, "in_stock", "sold");
        var service = new CheckoutService(context);

        var result = await service.ValidateCartAsync([firstItem.Id, secondItem.Id]);

        Assert.False(result.IsValid);
        Assert.Equal([secondItem.Id], result.ConflictItemIds);
        Assert.Null(result.Totals);
    }

    [Fact]
    public async Task SaveSaleAsync_CreatesSaleAndMarksItemsSold()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var (firstItem, secondItem) = await SeedTwoItemsAsync(context, "in_stock", "in_stock");
        var service = new CheckoutService(context);

        var result = await service.SaveSaleAsync([firstItem.Id, secondItem.Id]);

        Assert.True(result.Saved);
        Assert.NotNull(result.SaleId);
        Assert.Empty(result.ConflictItemIds);
        Assert.NotNull(result.Totals);
        Assert.Equal(1500, result.Totals!.SubtotalSellerPriceCents);
        Assert.Equal(150, result.Totals.CheckoutFeeCents);
        Assert.Equal(1650, result.Totals.TotalPaidCents);

        var sale = await context.Sales.Include(x => x.SaleLines).SingleAsync();
        Assert.Equal(result.SaleId, sale.Id);
        Assert.Equal(1500, sale.SubtotalSellerPriceCents);
        Assert.Equal(150, sale.CheckoutFeeCents);
        Assert.Equal(1650, sale.TotalPaidCents);
        Assert.Null(sale.ReceiptDetails);
        Assert.Equal(DateTimeKind.Utc, sale.SoldAt.Kind);

        Assert.Collection(
            sale.SaleLines.OrderBy(x => x.ItemId),
            firstLine =>
            {
                Assert.Equal(firstItem.Id, firstLine.ItemId);
                Assert.Equal(500, firstLine.SellerPriceAtSaleCents);
            },
            secondLine =>
            {
                Assert.Equal(secondItem.Id, secondLine.ItemId);
                Assert.Equal(1000, secondLine.SellerPriceAtSaleCents);
            });

        Assert.All(await context.Items.ToListAsync(), item => Assert.Equal("sold", item.Status));
    }

    [Fact]
    public async Task SaveSaleAsync_StoresTrimmedReceiptDetails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var (firstItem, _) = await SeedTwoItemsAsync(context, "in_stock", "in_stock");
        var service = new CheckoutService(context);

        var result = await service.SaveSaleAsync([firstItem.Id], "  Association sportive\n12 rue des Lilas  ");

        Assert.True(result.Saved);
        var sale = await context.Sales.SingleAsync();
        Assert.Equal("Association sportive\n12 rue des Lilas", sale.ReceiptDetails);
    }

    [Fact]
    public async Task SaveSaleAsync_StoresNullForBlankReceiptDetails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var (firstItem, _) = await SeedTwoItemsAsync(context, "in_stock", "in_stock");
        var service = new CheckoutService(context);

        var result = await service.SaveSaleAsync([firstItem.Id], " \r\n ");

        Assert.True(result.Saved);
        var sale = await context.Sales.SingleAsync();
        Assert.Null(sale.ReceiptDetails);
    }

    [Fact]
    public async Task SaveSaleAsync_RejectsUnavailableItemsWithoutPersistingPartialSale()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var (firstItem, secondItem) = await SeedTwoItemsAsync(context, "in_stock", "sold");
        var service = new CheckoutService(context);

        var result = await service.SaveSaleAsync([firstItem.Id, secondItem.Id]);

        Assert.False(result.Saved);
        Assert.Null(result.SaleId);
        Assert.Equal([secondItem.Id], result.ConflictItemIds);
        Assert.Null(result.Totals);
        Assert.Empty(await context.Sales.ToListAsync());
        Assert.Empty(await context.SaleLines.ToListAsync());
        Assert.Equal("in_stock", (await context.Items.SingleAsync(x => x.Id == firstItem.Id)).Status);
        Assert.Equal("sold", (await context.Items.SingleAsync(x => x.Id == secondItem.Id)).Status);
    }

    [Fact]
    public async Task SaveSaleAsync_RejectsNewSalesAfterPostSaleTransition()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var (firstItem, _) = await SeedTwoItemsAsync(context, "in_stock", "in_stock");
        var phaseService = new SalePhaseService(context);
        await phaseService.SetPhaseAsync(SalePhase.PostSale);
        var service = new CheckoutService(context);

        var result = await service.SaveSaleAsync([firstItem.Id]);

        Assert.False(result.Saved);
        Assert.Null(result.SaleId);
        Assert.Empty(result.ConflictItemIds);
        Assert.Equal("La vente est en après-vente. Aucune nouvelle vente ne peut être enregistrée.", result.Message);
        Assert.Empty(await context.Sales.ToListAsync());
        Assert.Equal("in_stock", (await context.Items.SingleAsync(x => x.Id == firstItem.Id)).Status);
    }

    private static async Task<(ItemEntity FirstItem, ItemEntity SecondItem)> SeedTwoItemsAsync(
        AppDbContext context,
        string firstStatus,
        string secondStatus)
    {
        var seller = new SellerEntity
        {
            SellerCode = "BB",
            FirstName = "Grace",
            LastName = "Hopper",
            Email = "grace@example.test"
        };

        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var firstItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "BB-001",
            ItemNumber = 1,
            Title = "Puzzle",
            SellerPriceCents = 500,
            Status = firstStatus
        };
        var secondItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "BB-002",
            ItemNumber = 2,
            Title = "Camion",
            SellerPriceCents = 1000,
            Status = secondStatus
        };

        context.Items.AddRange(firstItem, secondItem);
        await context.SaveChangesAsync();
        return (firstItem, secondItem);
    }
}
