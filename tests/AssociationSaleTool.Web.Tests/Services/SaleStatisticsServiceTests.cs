using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class SaleStatisticsServiceTests
{
    // Hand-computed dataset:
    // - seller A paid its 2 EUR fee; sold 15 EUR, missing 4 EUR, in stock 8 EUR, defective 25 EUR (refunded)
    //   payable 19 EUR, margin 2.85 EUR, owed 16.15 EUR
    // - seller B paid nothing; sold 1 EUR and 21 EUR: payable 22 EUR, margin 3.30 EUR, fee 2 EUR deducted, owed 16.70 EUR
    // - seller C paid nothing; sold 1 EUR: margin 0.15 EUR, only 0.85 EUR of its fee can be deducted, owed 0
    // - seller D never validated: one item without price, one at 0 EUR
    [Fact]
    public async Task GetStatisticsAsync_ComputesFeesMoneyItemsPricesAndCities()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var cashRegisterId = await SeedCashRegisterAsync(context);

        var sellerA = await SeedSellerAsync(context, "A", feePaidCents: 200);
        var a1 = await SeedItemAsync(context, sellerA, "A-001", 1500, "sold");
        await SeedItemAsync(context, sellerA, "A-002", 400, "missing");
        await SeedItemAsync(context, sellerA, "A-003", 800, "in_stock");
        var a4 = await SeedItemAsync(context, sellerA, "A-004", 2500, "defective");

        var sellerB = await SeedSellerAsync(context, "B", feePaidCents: 0);
        var b1 = await SeedItemAsync(context, sellerB, "B-001", 100, "sold");
        var b2 = await SeedItemAsync(context, sellerB, "B-002", 2100, "sold");

        var sellerC = await SeedSellerAsync(context, "C", feePaidCents: 0);
        var c1 = await SeedItemAsync(context, sellerC, "C-001", 100, "sold");

        var sellerD = await SeedSellerAsync(context, "D", feePaidCents: 0, depositStatus: "deposit_pending");
        await SeedItemAsync(context, sellerD, null, null, "registered");
        await SeedItemAsync(context, sellerD, null, 0, "registered");

        context.Sales.AddRange(
            new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 1600,
                CheckoutFeeCents = 160,
                TotalPaidCents = 1760,
                PaymentMethod = "card",
                BuyerCity = "Grenoble",
                SaleLines =
                [
                    new SaleLineEntity { ItemId = a1.Id, SellerPriceAtSaleCents = 1500 },
                    new SaleLineEntity { ItemId = b1.Id, SellerPriceAtSaleCents = 100 }
                ]
            },
            new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 2500,
                CheckoutFeeCents = 250,
                TotalPaidCents = 2750,
                PaymentMethod = "cash",
                BuyerCity = "grenoble ",
                SaleLines =
                [
                    new SaleLineEntity
                    {
                        ItemId = a4.Id,
                        SellerPriceAtSaleCents = 2500,
                        RefundedAt = DateTime.UtcNow,
                        RefundItemPriceCents = 2500,
                        RefundCheckoutFeeCents = 250,
                        RefundTotalCents = 2750,
                        RefundReason = "Cassé",
                        RefundCashRegisterId = cashRegisterId
                    }
                ]
            },
            new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 2200,
                CheckoutFeeCents = 220,
                TotalPaidCents = 2420,
                PaymentMethod = "cheque",
                ChequeNumber = "123",
                ChequeBankName = "LCL",
                BuyerCity = "Voiron",
                SaleLines =
                [
                    new SaleLineEntity { ItemId = b2.Id, SellerPriceAtSaleCents = 2100 },
                    new SaleLineEntity { ItemId = c1.Id, SellerPriceAtSaleCents = 100 }
                ]
            });
        await context.SaveChangesAsync();

        var statistics = await CreateService(context).GetStatisticsAsync();

        var fees = statistics.Fees;
        Assert.Equal(600, fees.FlatFeeExpectedCents);
        Assert.Equal(200, fees.FlatFeePaidAtDepositCents);
        Assert.Equal(285, fees.FlatFeeDeductedAtPayoutCents);
        Assert.Equal(115, fees.FlatFeeUncollectedCents);
        Assert.Equal(630, fees.SellerMarginCents);
        Assert.Equal(380, fees.ClientFeeCents);
        Assert.Equal(1495, fees.TotalCents);

        var clients = statistics.Clients;
        Assert.Equal(6930, clients.CollectedCents);
        Assert.Equal(2750, clients.RefundedCents);
        Assert.Equal(4180, clients.NetCents);
        Assert.Equal(1760, clients.CardCents);
        Assert.Equal(2750, clients.CashCents);
        Assert.Equal(2420, clients.ChequeCents);
        Assert.Equal(3, clients.SaleCount);

        var sellers = statistics.Sellers;
        Assert.Equal(3285, sellers.OwedCents);
        Assert.Equal(2, sellers.SellersToPayCount);
        Assert.Equal(1, sellers.MissingItemCount);
        Assert.Equal(400, sellers.MissingItemAmountCents);

        var items = statistics.Items;
        Assert.Equal(9, items.TotalCount);
        Assert.Equal(2, items.RegisteredCount);
        Assert.Equal(1, items.InStockCount);
        Assert.Equal(4, items.SoldCount);
        Assert.Equal(1, items.MissingCount);
        Assert.Equal(1, items.DefectiveCount);

        Assert.Collection(
            statistics.PriceBrackets,
            bracket => AssertBracket(bracket, "1 à 2 €", 2, 200, 2, 200),
            bracket => AssertBracket(bracket, "3 à 5 €", 1, 400, 0, 0),
            bracket => AssertBracket(bracket, "6 à 10 €", 1, 800, 0, 0),
            bracket => AssertBracket(bracket, "11 à 20 €", 1, 1500, 1, 1500),
            bracket => AssertBracket(bracket, "Plus de 20 €", 2, 4600, 1, 2100),
            bracket => AssertBracket(bracket, "Sans prix valide", 2, 0, 0, 0));

        Assert.Collection(
            statistics.Cities,
            city =>
            {
                Assert.Equal("Grenoble", city.City);
                Assert.Equal(2, city.SaleCount);
            },
            city =>
            {
                Assert.Equal("Voiron", city.City);
                Assert.Equal(1, city.SaleCount);
            });
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsZerosAndStandardBracketsWithoutData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);

        var statistics = await CreateService(context).GetStatisticsAsync();

        Assert.Equal(0, statistics.Fees.TotalCents);
        Assert.Equal(0, statistics.Clients.NetCents);
        Assert.Equal(0, statistics.Sellers.OwedCents);
        Assert.Equal(0, statistics.Items.TotalCount);
        Assert.Equal(
            ["1 à 2 €", "3 à 5 €", "6 à 10 €", "11 à 20 €", "Plus de 20 €"],
            statistics.PriceBrackets.Select(bracket => bracket.Label));
        Assert.Empty(statistics.Cities);
    }

    [Fact]
    public async Task GetStatisticsAsync_GroupsSalesWithoutCity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var cashRegisterId = await SeedCashRegisterAsync(context);
        context.Sales.AddRange(
            new SaleEntity { SoldAt = DateTime.UtcNow, CashRegisterId = cashRegisterId, PaymentMethod = "card", BuyerCity = null },
            new SaleEntity { SoldAt = DateTime.UtcNow, CashRegisterId = cashRegisterId, PaymentMethod = "card", BuyerCity = "  " });
        await context.SaveChangesAsync();

        var statistics = await CreateService(context).GetStatisticsAsync();

        var city = Assert.Single(statistics.Cities);
        Assert.Equal("Non renseignée", city.City);
        Assert.Equal(2, city.SaleCount);
    }

    private static void AssertBracket(
        AssociationSaleTool.Web.Services.Models.SaleStatisticsPriceBracket bracket,
        string label,
        int itemCount,
        int itemAmountCents,
        int soldCount,
        int soldAmountCents)
    {
        Assert.Equal(label, bracket.Label);
        Assert.Equal(itemCount, bracket.ItemCount);
        Assert.Equal(itemAmountCents, bracket.ItemAmountCents);
        Assert.Equal(soldCount, bracket.SoldCount);
        Assert.Equal(soldAmountCents, bracket.SoldAmountCents);
    }

    private static SaleStatisticsService CreateService(AppDbContext context)
    {
        return new SaleStatisticsService(context, new ReconciliationService(context));
    }

    private static async Task<SellerEntity> SeedSellerAsync(
        AppDbContext context,
        string sellerCode,
        int feePaidCents,
        string depositStatus = "deposit_validated")
    {
        var seller = new SellerEntity
        {
            SellerCode = sellerCode,
            FirstName = "Vendeur",
            LastName = sellerCode,
            FeePaidCents = feePaidCents,
            DepositStatus = depositStatus
        };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();
        return seller;
    }

    private static async Task<ItemEntity> SeedItemAsync(
        AppDbContext context,
        SellerEntity seller,
        string? reference,
        int? sellerPriceCents,
        string status)
    {
        var item = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = reference,
            Title = reference ?? "Sans référence",
            SellerPriceCents = sellerPriceCents,
            Status = status
        };
        context.Items.Add(item);
        await context.SaveChangesAsync();
        return item;
    }

    private static async Task<long> SeedCashRegisterAsync(AppDbContext context)
    {
        var cashRegister = new CashRegisterEntity { Name = "CAISSE-1", InitialCashCents = 5000, IsActive = true };
        context.CashRegisters.Add(cashRegister);
        await context.SaveChangesAsync();
        return cashRegister.Id;
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
