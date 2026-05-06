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
        await SeedItemAsync(context, seller, "DL001", 1, "Puzzle rouge", 800, "registered");
        await SeedItemAsync(context, seller, "DL002", 2, "Train vert", 1200, "in_stock");
        await SeedItemAsync(context, seller, "DL003", 3, "Camion vendu", 900, "sold");
        await SeedItemAsync(context, seller, "DL004", 4, "Jeu manquant", 600, "missing");
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
        await SeedItemAsync(context, seller, "ZX001", 1, "Article vendu", 1000, "sold");
        await SeedItemAsync(context, seller, "ZX002", 2, "Article retour", 1500, "in_stock");
        await SeedItemAsync(context, seller, "ZX003", 3, "Article defectueux", 500, "defective");
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
        var firstItem = await SeedItemAsync(context, seller, "SA001", 1, "Maison bois", 1000, "sold");
        var secondItem = await SeedItemAsync(context, seller, "SA002", 2, "Robot bleu", 1200, "sold");
        var sale = new SaleEntity
        {
            SoldAt = new DateTime(2026, 5, 5, 9, 30, 0, DateTimeKind.Utc),
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
        sale.SaleLines.Add(new SaleLineEntity { ItemId = firstItem.Id, SellerPriceAtSaleCents = firstItem.SellerPriceCents });
        sale.SaleLines.Add(new SaleLineEntity { ItemId = secondItem.Id, SellerPriceAtSaleCents = secondItem.SellerPriceCents });
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
        context.Sales.AddRange(
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc),
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
                SubtotalSellerPriceCents = 1200,
                CheckoutFeeCents = 120,
                TotalPaidCents = 1320,
                PaymentMethod = SalePaymentMethods.Cash
            },
            new SaleEntity
            {
                SoldAt = new DateTime(2026, 5, 5, 12, 0, 0, DateTimeKind.Utc),
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
        await context.SaveChangesAsync();
        return item;
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
