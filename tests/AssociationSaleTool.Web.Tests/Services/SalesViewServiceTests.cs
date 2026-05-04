using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class SalesViewServiceTests
{
    [Fact]
    public async Task ListSalesAsync_ReturnsNewestSalesWithSearchableText()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var olderSale = await SeedSaleAsync(
            context,
            new DateTime(2026, 5, 4, 8, 30, 0, DateTimeKind.Utc),
            "Association sportive",
            ("SV-001-001", "Puzzle historique", 800, null));
        var newerSale = await SeedSaleAsync(
            context,
            new DateTime(2026, 5, 4, 9, 45, 0, DateTimeKind.Utc),
            null,
            ("SV-002-001", "Camion rouge", 1200, null),
            ("SV-002-002", "Train bleu", 500, null));
        var service = new SalesViewService(context);

        var sales = await service.ListSalesAsync();

        Assert.Collection(
            sales,
            first =>
            {
                Assert.Equal(newerSale.Id, first.SaleId);
                Assert.Equal(2, first.ItemCount);
                Assert.Equal(1700, first.SubtotalSellerPriceCents);
                Assert.Equal(170, first.CheckoutFeeCents);
                Assert.Equal(1870, first.TotalPaidCents);
                Assert.False(first.HasReceiptDetails);
                Assert.Contains(newerSale.Id.ToString(), first.SearchText);
                Assert.Contains("sv-002-001", first.SearchText);
                Assert.Contains("camion rouge", first.SearchText);
                Assert.Contains("train bleu", first.SearchText);
            },
            second =>
            {
                Assert.Equal(olderSale.Id, second.SaleId);
                Assert.Equal(1, second.ItemCount);
                Assert.True(second.HasReceiptDetails);
                Assert.Contains("association sportive", second.SearchText);
            });
    }

    [Fact]
    public async Task GetSaleAsync_ReturnsReadonlySaleDetailWithCorrectedLines()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var correctedAt = new DateTime(2026, 5, 4, 11, 15, 0, DateTimeKind.Utc);
        var sale = await SeedSaleAsync(
            context,
            new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc),
            "Client recu",
            ("SV-003-001", "Jeu corrige", 900, correctedAt),
            ("SV-003-002", "Jeu conserve", 600, null));
        var service = new SalesViewService(context);

        var detail = await service.GetSaleAsync(sale.Id);

        Assert.NotNull(detail);
        Assert.Equal(sale.Id, detail!.SaleId);
        Assert.Equal("Client recu", detail.ReceiptDetails);
        Assert.Equal(1500, detail.SubtotalSellerPriceCents);
        Assert.Equal(150, detail.CheckoutFeeCents);
        Assert.Equal(1650, detail.TotalPaidCents);
        Assert.Collection(
            detail.Lines,
            first =>
            {
                Assert.Equal("SV-003-001", first.Reference);
                Assert.Equal("Jeu corrige", first.Title);
                Assert.Equal(900, first.SellerPriceAtSaleCents);
                Assert.Equal(correctedAt, first.CorrectedAt);
                Assert.True(first.IsCorrected);
            },
            second =>
            {
                Assert.Equal("SV-003-002", second.Reference);
                Assert.Equal("Jeu conserve", second.Title);
                Assert.Equal(600, second.SellerPriceAtSaleCents);
                Assert.Null(second.CorrectedAt);
                Assert.False(second.IsCorrected);
            });
    }

    [Fact]
    public async Task GetSaleAsync_ReturnsNullWhenSaleDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new SalesViewService(context);

        var detail = await service.GetSaleAsync(42);

        Assert.Null(detail);
    }

    private static async Task<SaleEntity> SeedSaleAsync(
        AppDbContext context,
        DateTime soldAt,
        string? receiptDetails,
        params (string Reference, string Title, int PriceCents, DateTime? CorrectedAt)[] lines)
    {
        var seller = new SellerEntity
        {
            SellerCode = $"SV-{context.Sellers.Count() + 1:000}",
            FirstName = "Sonia",
            LastName = "Vente"
        };

        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var sale = new SaleEntity
        {
            SoldAt = soldAt,
            SubtotalSellerPriceCents = lines.Sum(x => x.PriceCents),
            CheckoutFeeCents = lines.Sum(x => x.PriceCents) / 10,
            TotalPaidCents = lines.Sum(x => x.PriceCents) + lines.Sum(x => x.PriceCents) / 10,
            ReceiptDetails = receiptDetails
        };

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = line.Reference,
                ItemNumber = index + 1,
                Title = line.Title,
                SellerPriceCents = line.PriceCents,
                Status = line.CorrectedAt is null ? "sold" : "in_stock"
            };

            context.Items.Add(item);
            await context.SaveChangesAsync();

            sale.SaleLines.Add(new SaleLineEntity
            {
                ItemId = item.Id,
                SellerPriceAtSaleCents = line.PriceCents,
                CorrectedAt = line.CorrectedAt
            });
        }

        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        return sale;
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
