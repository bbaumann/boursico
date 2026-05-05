using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class DefectiveItemServiceTests
{
    [Fact]
    public async Task MarkDefectiveAsync_ChangesInStockItemToDefective()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var seller = await SeedSellerAsync(context);
        var item = AddItem(context, seller, "AA-001", 1, "Train", 500, "in_stock");
        await context.SaveChangesAsync();
        var service = new DefectiveItemService(context);

        var result = await service.MarkDefectiveAsync(item.Id);

        Assert.Equal(DefectiveItemStatusChangeResult.Changed, result);
        Assert.Equal("defective", await GetSavedStatusAsync(connection, item.Id));
    }

    [Fact]
    public async Task MarkInStockAsync_RestoresDefectiveItemWithoutRefundHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var seller = await SeedSellerAsync(context);
        var item = AddItem(context, seller, "AA-001", 1, "Train", 500, "defective");
        await context.SaveChangesAsync();
        var service = new DefectiveItemService(context);

        var result = await service.MarkInStockAsync(item.Id);

        Assert.Equal(DefectiveItemStatusChangeResult.Changed, result);
        Assert.Equal("in_stock", await GetSavedStatusAsync(connection, item.Id));
    }

    [Fact]
    public async Task MarkInStockAsync_RejectsDefectiveItemWithRefundHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var seller = await SeedSellerAsync(context);
        var item = AddItem(context, seller, "AA-001", 1, "Train", 500, "defective");
        await context.SaveChangesAsync();
        context.Sales.Add(new SaleEntity
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
                    SellerPriceAtSaleCents = 500,
                    RefundedAt = DateTime.UtcNow,
                    RefundItemPriceCents = 500,
                    RefundCheckoutFeeCents = 50,
                    RefundTotalCents = 550,
                    RefundReason = "defective"
                }
            ]
        });
        await context.SaveChangesAsync();
        var service = new DefectiveItemService(context);

        var result = await service.MarkInStockAsync(item.Id);

        Assert.Equal(DefectiveItemStatusChangeResult.NotAllowed, result);
        Assert.Equal("defective", await GetSavedStatusAsync(connection, item.Id));
    }

    [Fact]
    public async Task SearchCandidatesAsync_FindsDefectCandidatesByReferenceTitleAndSeller()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var firstSeller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
        var secondSeller = new SellerEntity { SellerCode = "BB", FirstName = "Grace", LastName = "Hopper" };
        context.Sellers.AddRange(firstSeller, secondSeller);
        await context.SaveChangesAsync();
        var referenceMatch = AddItem(context, firstSeller, "AA-001", 1, "Train", 500, "in_stock");
        var titleMatch = AddItem(context, firstSeller, "AA-002", 2, "Puzzle bois", 600, "defective");
        var sellerMatch = AddItem(context, secondSeller, "BB-001", 1, "Camion", 700, "in_stock");
        AddItem(context, firstSeller, "AA-003", 3, "Livre", 800, "registered");
        await context.SaveChangesAsync();
        var service = new DefectiveItemService(context);

        var referenceResults = await service.SearchCandidatesAsync("001");
        var titleResults = await service.SearchCandidatesAsync("puzzle");
        var sellerResults = await service.SearchCandidatesAsync("hopper");

        Assert.Contains(referenceResults, item => item.ItemId == referenceMatch.Id);
        Assert.Contains(titleResults, item => item.ItemId == titleMatch.Id && item.Status == "defective");
        var sellerResult = Assert.Single(sellerResults);
        Assert.Equal(sellerMatch.Id, sellerResult.ItemId);
        Assert.Equal("BB", sellerResult.SellerCode);
        Assert.Equal("Grace Hopper", sellerResult.SellerName);
        Assert.Equal(700, sellerResult.SellerPriceCents);
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

    private static async Task<SellerEntity> SeedSellerAsync(AppDbContext context)
    {
        var seller = new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Ada",
            LastName = "Lovelace"
        };

        context.Sellers.Add(seller);
        await context.SaveChangesAsync();
        return seller;
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

    private static async Task<string> GetSavedStatusAsync(SqliteConnection connection, long itemId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        return await context.Items
            .AsNoTracking()
            .Where(x => x.Id == itemId)
            .Select(x => x.Status)
            .SingleAsync();
    }
}
