using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
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
            SalePaymentMethods.Cheque,
            "987654",
            "Crédit Mutuel",
            ("SV-001-001", "Puzzle historique", 800, null));
        var newerSale = await SeedSaleAsync(
            context,
            new DateTime(2026, 5, 4, 9, 45, 0, DateTimeKind.Utc),
            null,
            SalePaymentMethods.Cash,
            null,
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
                Assert.False(first.HasBuyerAddress);
                Assert.Equal("Espèces", first.PaymentMethodLabel);
                Assert.Contains(newerSale.Id.ToString(), first.SearchText);
                Assert.Contains("espèces", first.SearchText);
                Assert.Contains("sv-002-001", first.SearchText);
                Assert.Contains("camion rouge", first.SearchText);
                Assert.Contains("train bleu", first.SearchText);
            },
            second =>
            {
                Assert.Equal(olderSale.Id, second.SaleId);
                Assert.Equal(1, second.ItemCount);
                Assert.True(second.HasBuyerAddress);
                Assert.Equal("Chèque", second.PaymentMethodLabel);
                Assert.Contains("association sportive", second.SearchText);
                Assert.Contains("987654", second.SearchText);
                Assert.Contains("crédit mutuel", second.SearchText);
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
            SalePaymentMethods.Cheque,
            "CH-42",
            "Banque Postale",
            ("SV-003-001", "Jeu corrige", 900, correctedAt),
            ("SV-003-002", "Jeu conserve", 600, null));
        var service = new SalesViewService(context);

        var detail = await service.GetSaleAsync(sale.Id);

        Assert.NotNull(detail);
        Assert.Equal(sale.Id, detail!.SaleId);
        Assert.Equal("Client recu", detail.BuyerAddress);
        Assert.Equal(1500, detail.SubtotalSellerPriceCents);
        Assert.Equal(150, detail.CheckoutFeeCents);
        Assert.Equal(1650, detail.TotalPaidCents);
        Assert.Equal("Chèque", detail.PaymentMethodLabel);
        Assert.Equal("CH-42", detail.ChequeNumber);
        Assert.Equal("Banque Postale", detail.ChequeBankName);
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
    public async Task SalesViewsIncludeBuyerDetails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var sale = await SeedSaleAsync(
            context,
            new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc),
            null,
            SalePaymentMethods.Card,
            null,
            null,
            ("SV-004-001", "Jeu acheteur", 900, null));
        sale.BuyerName = "Ecole du centre";
        sale.BuyerPostalCode = "38100";
        sale.BuyerCity = "Grenoble";
        await context.SaveChangesAsync();
        var service = new SalesViewService(context);

        var listItem = Assert.Single(await service.ListSalesAsync());
        var detail = await service.GetSaleAsync(sale.Id);

        Assert.True(listItem.HasBuyerDetails);
        Assert.Equal("Ecole du centre - 38100 Grenoble", listItem.BuyerDisplay);
        Assert.Contains("ecole du centre", listItem.SearchText);
        Assert.Contains("38100", listItem.SearchText);
        Assert.Contains("grenoble", listItem.SearchText);
        Assert.NotNull(detail);
        Assert.True(detail!.HasBuyerDetails);
        Assert.Equal("Ecole du centre - 38100 Grenoble", detail.BuyerDisplay);
    }

    [Fact]
    public async Task GetSaleAsync_ReturnsReadonlySaleDetailWithRefundedDefectiveLines()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var refundedAt = new DateTime(2026, 5, 4, 15, 30, 0, DateTimeKind.Utc);
        var seller = new SellerEntity
        {
            SellerCode = "SV-005",
            FirstName = "Sonia",
            LastName = "Vente"
        };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var refundedItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "SV-005-001",
            ItemNumber = 1,
            Title = "Jeu defectueux",
            SellerPriceCents = 1200,
            Status = "defective"
        };
        var activeItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "SV-005-002",
            ItemNumber = 2,
            Title = "Jeu conserve",
            SellerPriceCents = 800,
            Status = "sold"
        };
        context.Items.AddRange(refundedItem, activeItem);
        await context.SaveChangesAsync();

        var sale = new SaleEntity
        {
            SoldAt = new DateTime(2026, 5, 4, 14, 0, 0, DateTimeKind.Utc),
            SubtotalSellerPriceCents = 2000,
            CheckoutFeeCents = 200,
            TotalPaidCents = 2200,
            SaleLines =
            [
                new SaleLineEntity
                {
                    ItemId = refundedItem.Id,
                    SellerPriceAtSaleCents = 1200,
                    RefundedAt = refundedAt,
                    RefundItemPriceCents = 1200,
                    RefundCheckoutFeeCents = 120,
                    RefundTotalCents = 1320,
                    RefundReason = "defective"
                },
                new SaleLineEntity
                {
                    ItemId = activeItem.Id,
                    SellerPriceAtSaleCents = 800
                }
            ]
        };
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        var service = new SalesViewService(context);

        var detail = await service.GetSaleAsync(sale.Id);

        Assert.NotNull(detail);
        Assert.Equal(2000, detail!.SubtotalSellerPriceCents);
        Assert.Equal(200, detail.CheckoutFeeCents);
        Assert.Equal(2200, detail.TotalPaidCents);
        Assert.True(detail.HasRefunds);
        Assert.Equal(1320, detail.RefundTotalCents);
        Assert.Equal(880, detail.CorrectedTotalPaidCents);
        Assert.Collection(
            detail.Lines,
            first =>
            {
                Assert.Equal("SV-005-001", first.Reference);
                Assert.False(first.IsCorrected);
                Assert.True(first.IsRefunded);
                Assert.Equal(refundedAt, first.RefundedAt);
                Assert.Equal(1200, first.RefundItemPriceCents);
                Assert.Equal(120, first.RefundCheckoutFeeCents);
                Assert.Equal(1320, first.RefundTotalCents);
                Assert.Equal("Rembours\u00e9", first.RefundStatusLabel);
            },
            second =>
            {
                Assert.Equal("SV-005-002", second.Reference);
                Assert.False(second.IsRefunded);
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
        string? buyerAddress,
        string? paymentMethod = SalePaymentMethods.Card,
        string? chequeNumber = null,
        string? chequeBankName = null,
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
            BuyerAddress = buyerAddress,
            PaymentMethod = paymentMethod,
            ChequeNumber = chequeNumber,
            ChequeBankName = chequeBankName
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
