using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class DataListServiceTests
{
    [Fact]
    public async Task ListItemsAsync_ReturnsAllStatusesWithSellerContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var seller = await SeedSellerAsync(context, "DL", "Diane", "Liste");
        await SeedItemAsync(context, seller, "DL001", "Puzzle rouge", 800, "registered");
        await SeedItemAsync(context, seller, "DL002", "Train vert", 1200, "in_stock");
        await SeedItemAsync(context, seller, "DL003", "Camion vendu", 900, "sold");
        await SeedItemAsync(context, seller, "DL004", "Jeu manquant", 600, "missing");
        var service = new DataListService(context);

        var items = await service.ListItemsAsync();

        Assert.Collection(
            items,
            first =>
            {
                Assert.Equal("DL001", first.Reference);
                Assert.Equal("registered", first.Status);
                Assert.Equal("enregistr\u00e9", first.StatusLabel);
                Assert.Equal("DL", first.SellerCode);
                Assert.Equal("Diane Liste", first.SellerName);
                Assert.Contains("puzzle rouge", first.SearchText);
                Assert.Contains("diane liste", first.SearchText);
            },
            second => Assert.Equal("in_stock", second.Status),
            third => Assert.Equal("sold", third.Status),
            fourth => Assert.Equal("missing", fourth.Status));
    }

    [Fact]
    public async Task ListSellersAsync_SearchTextContainsCodeAndNameOnly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var seller = await SeedSellerAsync(
            context,
            "ZX",
            "Zo\u00e9",
            "Martin",
            email: "zoe@example.test",
            phone: "0601020304",
            postalAddress: "8 rue de la Paix",
            feePaidCents: 200);
        await SeedItemAsync(context, seller, "ZX001", "Article vendu", 1000, "sold");
        await SeedItemAsync(context, seller, "ZX002", "Article retour", 1500, "in_stock");
        await SeedItemAsync(context, seller, "ZX003", "Article defectueux", 500, "defective");
        var service = new DataListService(context);

        var row = Assert.Single(await service.ListSellersAsync());

        Assert.Equal("ZX", row.SellerCode);
        Assert.Equal("Zo\u00e9 Martin", row.SellerName);
        Assert.Equal(3, row.ItemCount);
        Assert.Equal(1, row.InStockCount);
        Assert.Equal(1, row.SoldCount);
        Assert.Equal(1, row.DefectiveCount);
        Assert.Equal(200, row.ExpectedFeeCents);
        Assert.Equal(200, row.PaidFeeCents);
        Assert.Equal(0, row.RemainingFeeCents);
        Assert.Contains("zx", row.SearchText);
        Assert.Contains("zo\u00e9 martin", row.SearchText);
        Assert.DoesNotContain("0601020304", row.SearchText);
        Assert.DoesNotContain("zoe@example", row.SearchText);
        Assert.DoesNotContain("paix", row.SearchText);
    }

    [Fact]
    public async Task ListSalesAsync_SearchTextUsesApprovedSaleCriteriaOnly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var seller = await SeedSellerAsync(context, "SA", "Samira", "Article");
        var cashRegisterId = await SeedCashRegisterAsync(context, "CAISSE-SALES");
        var firstItem = await SeedItemAsync(context, seller, "SA001", "Maison bois", 1000, "sold");
        var secondItem = await SeedItemAsync(context, seller, "SA002", "Robot bleu", 1200, "sold");
        var sale = new SaleEntity
        {
            SoldAt = new DateTime(2026, 5, 5, 9, 30, 0, DateTimeKind.Utc),
            CashRegisterId = cashRegisterId,
            SubtotalSellerPriceCents = 2200,
            CheckoutFeeCents = 220,
            TotalPaidCents = 2420,
            BuyerName = "MJC Centre",
            BuyerPostalCode = "38100",
            BuyerCity = "Grenoble",
            BuyerAddress = "Texte re\u00e7u confidentiel",
            PaymentMethod = SalePaymentMethods.Cheque,
            ChequeNumber = "CH-999",
            ChequeBankName = "Banque cach\u00e9e"
        };
        sale.SaleLines.Add(new SaleLineEntity { ItemId = firstItem.Id, SellerPriceAtSaleCents = firstItem.SellerPriceCents.GetValueOrDefault() });
        sale.SaleLines.Add(new SaleLineEntity { ItemId = secondItem.Id, SellerPriceAtSaleCents = secondItem.SellerPriceCents.GetValueOrDefault() });
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        var service = new DataListService(context);

        var row = Assert.Single(await service.ListSalesAsync());

        Assert.Equal(sale.Id, row.SaleId);
        Assert.Equal("MJC Centre - 38100 Grenoble", row.BuyerDisplay);
        Assert.Equal("Ch\u00e8que", row.PaymentMethodLabel);
        Assert.Contains(sale.Id.ToString(), row.SearchText);
        Assert.Contains("mjc centre", row.SearchText);
        Assert.Contains("38100", row.SearchText);
        Assert.Contains("grenoble", row.SearchText);
        Assert.Contains("ch\u00e8que", row.SearchText);
        Assert.Contains("sa001", row.SearchText);
        Assert.Contains("maison bois", row.SearchText);
        Assert.Contains("robot bleu", row.SearchText);
        Assert.DoesNotContain("confidentiel", row.SearchText);
        Assert.DoesNotContain("ch-999", row.SearchText);
        Assert.DoesNotContain("banque cach\u00e9e", row.SearchText);
    }

    [Fact]
    public async Task ListChequesAsync_ReturnsOnlyChequeSalesWithRegisteredDetails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var cashRegisterId = await SeedCashRegisterAsync(context, "CAISSE-CHEQUES");
        context.Sales.AddRange(
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc),
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 3000,
                CheckoutFeeCents = 300,
                TotalPaidCents = 3300,
                PaymentMethod = SalePaymentMethods.Cheque,
                ChequeNumber = "CH-001",
                ChequeBankName = "Banque principale"
            },
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 11, 0, 0, DateTimeKind.Utc),
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 1200,
                CheckoutFeeCents = 120,
                TotalPaidCents = 1320,
                PaymentMethod = SalePaymentMethods.Cash
            },
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc),
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 2000,
                CheckoutFeeCents = 200,
                TotalPaidCents = 2200,
                PaymentMethod = SalePaymentMethods.Cheque,
                ChequeNumber = " ",
                ChequeBankName = "Banque incompl\u00e8te"
            });
        await context.SaveChangesAsync();
        var service = new DataListService(context);

        var row = Assert.Single(await service.ListChequesAsync());

        Assert.Equal("CH-001", row.ChequeNumber);
        Assert.Equal("Banque principale", row.ChequeBankName);
        Assert.Equal(3300, row.TotalPaidCents);
        Assert.Contains("ch-001", row.SearchText);
        Assert.Contains("banque principale", row.SearchText);
        Assert.DoesNotContain("banque incompl\u00e8te", row.SearchText);
    }

    [Fact]
    public async Task ListChequesByCashRegisterAsync_GroupsEveryChequeSaleUnderItsCashRegister()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var firstCashRegisterId = await SeedCashRegisterAsync(context, "CAISSE-1", displayOrder: 1);
        var secondCashRegisterId = await SeedCashRegisterAsync(context, "CAISSE-2", displayOrder: 2);
        await SeedCashRegisterAsync(context, "CAISSE-3", displayOrder: 3);
        context.Sales.AddRange(
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc),
                CashRegisterId = firstCashRegisterId,
                TotalPaidCents = 3300,
                PaymentMethod = SalePaymentMethods.Cheque,
                ChequeNumber = "0002",
                ChequeBankName = "Banque B",
                BuyerName = " Alice Martin "
            },
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 11, 0, 0, DateTimeKind.Utc),
                CashRegisterId = firstCashRegisterId,
                TotalPaidCents = 1100,
                PaymentMethod = SalePaymentMethods.Cheque,
                ChequeNumber = "0009",
                ChequeBankName = "Banque A"
            },
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc),
                CashRegisterId = firstCashRegisterId,
                TotalPaidCents = 1320,
                PaymentMethod = SalePaymentMethods.Cash
            },
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 13, 0, 0, DateTimeKind.Utc),
                CashRegisterId = secondCashRegisterId,
                TotalPaidCents = 2200,
                PaymentMethod = SalePaymentMethods.Cheque,
                ChequeNumber = " ",
                ChequeBankName = null,
                BuyerName = "Bruno Durand"
            });
        await context.SaveChangesAsync();
        var service = new DataListService(context);

        var groups = await service.ListChequesByCashRegisterAsync();

        Assert.Collection(
            groups,
            first =>
            {
                Assert.Equal("CAISSE-1", first.CashRegisterName);
                Assert.Equal(4400, first.TotalCents);
                Assert.Collection(
                    first.Cheques,
                    cheque =>
                    {
                        Assert.Equal("Banque A", cheque.ChequeBankName);
                        Assert.Equal("0009", cheque.ChequeNumber);
                        Assert.Equal(1100, cheque.TotalPaidCents);
                        Assert.Null(cheque.BuyerName);
                    },
                    cheque =>
                    {
                        Assert.Equal("Banque B", cheque.ChequeBankName);
                        Assert.Equal("0002", cheque.ChequeNumber);
                        Assert.Equal(3300, cheque.TotalPaidCents);
                        Assert.Equal("Alice Martin", cheque.BuyerName);
                    });
            },
            second =>
            {
                Assert.Equal("CAISSE-2", second.CashRegisterName);
                Assert.Equal(2200, second.TotalCents);
                var cheque = Assert.Single(second.Cheques);
                Assert.Null(cheque.ChequeBankName);
                Assert.Null(cheque.ChequeNumber);
                Assert.Equal("Bruno Durand", cheque.BuyerName);
            },
            third =>
            {
                Assert.Equal("CAISSE-3", third.CashRegisterName);
                Assert.Empty(third.Cheques);
                Assert.Equal(0, third.TotalCents);
            });
    }

    [Fact]
    public async Task ListClosingAsync_ReturnsPerCashRegisterThenGlobalMoneyTotals()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var seller = await SeedSellerAsync(context, "CL", "Claire", "Cloture");
        var firstItem = await SeedItemAsync(context, seller, "CL001", "Jeu carte", 1000, "defective");
        var secondItem = await SeedItemAsync(context, seller, "CL002", "Jeu especes", 2000, "sold");
        var thirdItem = await SeedItemAsync(context, seller, "CL003", "Jeu cheque", 3000, "sold");
        var firstCashRegisterId = await SeedCashRegisterAsync(context, "CAISSE-1", 5000, displayOrder: 1);
        var secondCashRegisterId = await SeedCashRegisterAsync(context, "CAISSE-2", 2000, displayOrder: 2);
        context.Sales.AddRange(
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc),
                CashRegisterId = firstCashRegisterId,
                SubtotalSellerPriceCents = 3000,
                CheckoutFeeCents = 300,
                TotalPaidCents = 3300,
                PaymentMethod = SalePaymentMethods.Card,
                SaleLines =
                [
                    new SaleLineEntity
                    {
                        ItemId = firstItem.Id,
                        SellerPriceAtSaleCents = firstItem.SellerPriceCents.GetValueOrDefault(),
                        RefundedAt = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc),
                        RefundItemPriceCents = 1000,
                        RefundCheckoutFeeCents = 100,
                        RefundTotalCents = 1100,
                        RefundReason = "defective",
                        RefundCashRegisterId = secondCashRegisterId
                    },
                    new SaleLineEntity { ItemId = secondItem.Id, SellerPriceAtSaleCents = secondItem.SellerPriceCents.GetValueOrDefault() }
                ]
            },
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 11, 0, 0, DateTimeKind.Utc),
                CashRegisterId = secondCashRegisterId,
                SubtotalSellerPriceCents = 3000,
                CheckoutFeeCents = 300,
                TotalPaidCents = 3300,
                PaymentMethod = SalePaymentMethods.Cheque,
                SaleLines =
                [
                    new SaleLineEntity { ItemId = thirdItem.Id, SellerPriceAtSaleCents = thirdItem.SellerPriceCents.GetValueOrDefault() }
                ]
            });
        await context.SaveChangesAsync();
        var service = new DataListService(context);

        var rows = await service.ListClosingAsync();

        Assert.Collection(
            rows,
            firstCheckout =>
            {
                Assert.False(firstCheckout.IsGlobal);
                Assert.Equal(firstCashRegisterId, firstCheckout.CashRegisterId);
                Assert.Equal("CAISSE-1", firstCheckout.Label);
                Assert.Equal(1, firstCheckout.SaleCount);
                Assert.Equal(2, firstCheckout.ItemCount);
                Assert.Equal(3300, firstCheckout.TotalCollectedCents);
                Assert.Equal(0, firstCheckout.RefundTotalCents);
                Assert.Equal(3300, firstCheckout.NetCollectedCents);
                Assert.Equal(3300, firstCheckout.CardTotalCents);
                Assert.Equal(5000, firstCheckout.InitialCashCents);
                Assert.Equal(5000, firstCheckout.ExpectedCashCents);
            },
            secondCheckout =>
            {
                Assert.False(secondCheckout.IsGlobal);
                Assert.Equal(secondCashRegisterId, secondCheckout.CashRegisterId);
                Assert.Equal("CAISSE-2", secondCheckout.Label);
                Assert.Equal(1, secondCheckout.SaleCount);
                Assert.Equal(1, secondCheckout.ItemCount);
                Assert.Equal(3300, secondCheckout.TotalCollectedCents);
                Assert.Equal(1100, secondCheckout.RefundTotalCents);
                Assert.Equal(2200, secondCheckout.NetCollectedCents);
                Assert.Equal(3300, secondCheckout.ChequeTotalCents);
                Assert.Equal(2000, secondCheckout.InitialCashCents);
                Assert.Equal(900, secondCheckout.ExpectedCashCents);
            },
            global =>
            {
                Assert.True(global.IsGlobal);
                Assert.Equal("Toutes les caisses", global.Label);
                Assert.Equal(2, global.SaleCount);
                Assert.Equal(3, global.ItemCount);
                Assert.Equal(6000, global.SubtotalSellerPriceCents);
                Assert.Equal(600, global.CheckoutFeeCents);
                Assert.Equal(6600, global.TotalCollectedCents);
                Assert.Equal(1100, global.RefundTotalCents);
                Assert.Equal(5500, global.NetCollectedCents);
                Assert.Equal(3300, global.CardTotalCents);
                Assert.Equal(0, global.CashTotalCents);
                Assert.Equal(3300, global.ChequeTotalCents);
                Assert.Equal(7000, global.InitialCashCents);
                Assert.Equal(5900, global.ExpectedCashCents);
            });
    }

    private static async Task<SellerEntity> SeedSellerAsync(
        AppDbContext context,
        string sellerCode,
        string firstName,
        string lastName,
        string? email = null,
        string phone = "0611111111",
        string postalAddress = "1 rue Exemple",
        int feePaidCents = 0)
    {
        var seller = new SellerEntity
        {
            SellerCode = sellerCode,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = phone,
            PostalAddress = postalAddress,
            FeePaidCents = feePaidCents
        };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();
        return seller;
    }

    private static async Task<ItemEntity> SeedItemAsync(
        AppDbContext context,
        SellerEntity seller,
        string reference,
        string title,
        int sellerPriceCents,
        string status)
    {
        var item = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = reference,
            Title = title,
            SellerPriceCents = sellerPriceCents,
            Status = status
        };
        context.Items.Add(item);
        await context.SaveChangesAsync();
        return item;
    }

    private static async Task<long> SeedCashRegisterAsync(
        AppDbContext context,
        string name,
        int initialCashCents = 5000,
        bool isActive = true,
        int displayOrder = 0)
    {
        var existingCashRegister = await context.CashRegisters.SingleOrDefaultAsync(x => x.Name == name);
        if (existingCashRegister is not null)
        {
            return existingCashRegister.Id;
        }

        var cashRegister = new CashRegisterEntity
        {
            Name = name,
            InitialCashCents = initialCashCents,
            IsActive = isActive,
            DisplayOrder = displayOrder
        };
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
