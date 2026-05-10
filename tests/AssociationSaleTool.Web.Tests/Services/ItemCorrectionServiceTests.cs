using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class ItemCorrectionServiceTests
{
    [Fact]
    public async Task ListCandidatesAsync_ReturnsAllNonRegisteredItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var seller = await SeedSellerAsync(context);
        AddItem(context, seller, "AA-000", 0, "Registered hidden", 100, "registered");

        for (var index = 1; index <= 16; index++)
        {
            AddItem(context, seller, $"AA-{index:000}", index, $"Visible {index}", 100 + index, "in_stock");
        }

        await context.SaveChangesAsync();
        var service = new ItemCorrectionService(context);

        var candidates = await service.ListCandidatesAsync();

        Assert.Equal(16, candidates.Count);
        Assert.DoesNotContain(candidates, item => item.Reference == "AA-000");
        Assert.Equal("AA-001", candidates.First().Reference);
        Assert.Equal("AA-016", candidates.Last().Reference);
    }

    [Fact]
    public async Task ListCandidatesAsync_ProjectsSoldItemRefundAndCorrectionData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var seller = await SeedSellerAsync(context);
        var item = AddItem(context, seller, "AA-001", 1, "Sold toy", 1200, "sold");
        await context.SaveChangesAsync();
        var sale = new SaleEntity
        {
            SoldAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc),
            SubtotalSellerPriceCents = 1200,
            CheckoutFeeCents = 120,
            TotalPaidCents = 1320,
            SaleLines =
            [
                new SaleLineEntity
                {
                    ItemId = item.Id,
                    SellerPriceAtSaleCents = 1200
                }
            ]
        };
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        var service = new ItemCorrectionService(context);

        var candidate = Assert.Single(await service.ListCandidatesAsync());

        Assert.Equal(item.Id, candidate.ItemId);
        Assert.Equal(sale.Id, candidate.SaleId);
        Assert.Equal(sale.SaleLines.Single().Id, candidate.SaleLineId);
        Assert.Equal("sold", candidate.Status);
        Assert.True(candidate.CanCorrectSaleLine);
        Assert.True(candidate.CanRefundDefective);
        Assert.False(candidate.CanMarkDefective);
        Assert.Equal(120, candidate.RefundCheckoutFeeCents);
        Assert.Equal(1320, candidate.RefundTotalCents);
    }

    [Fact]
    public async Task ListCandidatesAsync_ProjectsRefundUndoDataForRefundedDefectiveItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var seller = await SeedSellerAsync(context);
        var item = AddItem(context, seller, "AA-001", 1, "Refunded toy", 1200, "defective");
        await context.SaveChangesAsync();
        var sale = new SaleEntity
        {
            SoldAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc),
            SubtotalSellerPriceCents = 1200,
            CheckoutFeeCents = 120,
            TotalPaidCents = 1320,
            SaleLines =
            [
                new SaleLineEntity
                {
                    ItemId = item.Id,
                    SellerPriceAtSaleCents = 1200,
                    RefundedAt = new DateTime(2026, 5, 5, 11, 0, 0, DateTimeKind.Utc),
                    RefundItemPriceCents = 1200,
                    RefundCheckoutFeeCents = 120,
                    RefundTotalCents = 1320,
                    RefundReason = "defective"
                }
            ]
        };
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        var service = new ItemCorrectionService(context);

        var candidate = Assert.Single(await service.ListCandidatesAsync());

        Assert.Equal(item.Id, candidate.ItemId);
        Assert.Equal(sale.Id, candidate.SaleId);
        Assert.Equal(sale.SaleLines.Single().Id, candidate.RefundedSaleLineId);
        Assert.Null(candidate.SaleLineId);
        Assert.Equal("defective", candidate.Status);
        Assert.True(candidate.CanCancelRefund);
        Assert.False(candidate.CanRestoreInStock);
        Assert.Equal(1200, candidate.DisplayPriceCents);
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
}
