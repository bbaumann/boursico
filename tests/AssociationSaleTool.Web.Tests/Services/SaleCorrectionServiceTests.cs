using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class SaleCorrectionServiceTests
{
    [Fact]
    public async Task CorrectSaleLineAsync_ReturnsItemToStockAndKeepsCorrectedSaleLine()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var (item, saleLine) = await SeedSoldItemAsync(context);
        var service = new SaleCorrectionService(context);

        var result = await service.CorrectSaleLineAsync(saleLine.Id);

        Assert.Equal(SaleCorrectionResult.Corrected, result);
        var savedItem = await context.Items.SingleAsync(x => x.Id == item.Id);
        var correctedLine = await context.SaleLines.SingleAsync(x => x.Id == saleLine.Id);
        Assert.Equal("in_stock", savedItem.Status);
        Assert.NotNull(correctedLine.CorrectedAt);
    }

    [Fact]
    public async Task CorrectSaleLineAsync_AllowsTheItemToBeSoldAgain()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var (item, saleLine) = await SeedSoldItemAsync(context);
        var correctionService = new SaleCorrectionService(context);
        var checkoutService = new CheckoutService(context);

        await correctionService.CorrectSaleLineAsync(saleLine.Id);
        var result = await checkoutService.SaveSaleAsync(
            [item.Id],
            buyerName: "Client correction",
            buyerPostalCode: "38000",
            buyerCity: "Grenoble");

        Assert.True(result.Saved);
        Assert.Equal("sold", (await context.Items.SingleAsync(x => x.Id == item.Id)).Status);
        Assert.Equal(2, await context.SaleLines.CountAsync(x => x.ItemId == item.Id));
        Assert.Single(await context.SaleLines.Where(x => x.ItemId == item.Id && x.CorrectedAt == null).ToListAsync());
    }

    [Fact]
    public async Task ListActiveSoldItemsAsync_ReturnsOnlyUncorrectedSoldLines()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var (_, saleLine) = await SeedSoldItemAsync(context);
        var service = new SaleCorrectionService(context);

        var beforeCorrection = await service.ListActiveSoldItemsAsync();
        await service.CorrectSaleLineAsync(saleLine.Id);
        var afterCorrection = await service.ListActiveSoldItemsAsync();

        var item = Assert.Single(beforeCorrection);
        Assert.Equal(saleLine.Id, item.SaleLineId);
        Assert.Empty(afterCorrection);
    }

    [Fact]
    public async Task CorrectSaleLineAsync_ReturnsNotAllowedForAlreadyCorrectedLine()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var (_, saleLine) = await SeedSoldItemAsync(context);
        var service = new SaleCorrectionService(context);
        await service.CorrectSaleLineAsync(saleLine.Id);

        var result = await service.CorrectSaleLineAsync(saleLine.Id);

        Assert.Equal(SaleCorrectionResult.NotAllowed, result);
    }

    private static async Task<(ItemEntity Item, SaleLineEntity SaleLine)> SeedSoldItemAsync(AppDbContext context)
    {
        var seller = new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Ada",
            LastName = "Lovelace"
        };

        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var item = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-001",
            ItemNumber = 1,
            Title = "Train",
            SellerPriceCents = 500,
            Status = "sold"
        };

        context.Items.Add(item);
        await context.SaveChangesAsync();

        var sale = new SaleEntity
        {
            SoldAt = DateTime.UtcNow,
            SubtotalSellerPriceCents = 500,
            CheckoutFeeCents = 50,
            TotalPaidCents = 550,
            SaleLines =
            [
                new SaleLineEntity
                {
                    ItemId = item.Id,
                    SellerPriceAtSaleCents = 500
                }
            ]
        };

        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        return (item, sale.SaleLines.Single());
    }

    private static async Task<AppDbContext> CreateContextAsync(SqliteConnection connection)
    {
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
