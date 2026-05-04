using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class SellerDuplicateServiceTests
{
    [Fact]
    public async Task FindDuplicateSellersAsync_MatchesNamesCaseSpacesAndAccents()
    {
        await using var context = await CreateContextAsync();
        context.Sellers.Add(new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Émilie",
            LastName = "Du  Pont",
            Phone = "0123"
        });
        await context.SaveChangesAsync();

        var service = new SellerDuplicateService(context);

        var matches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest(" emilie ", "du pont", "0123"));

        var match = Assert.Single(matches);
        Assert.Equal("AA", match.SellerCode);
        Assert.Equal(SellerDuplicateMatchReason.NameAndPhone, match.Reason);
    }

    [Fact]
    public async Task FindDuplicateSellersAsync_DoesNotCollapseHyphensOrSpaces()
    {
        await using var context = await CreateContextAsync();
        context.Sellers.Add(new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Jean-Luc",
            LastName = "Martin",
            Phone = "0123"
        });
        await context.SaveChangesAsync();

        var service = new SellerDuplicateService(context);

        var matches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Jean Luc", "Martin", "0123"));

        Assert.Empty(matches);
    }

    [Fact]
    public async Task FindDuplicateSellersAsync_TrimsPhoneButKeepsFormattingMeaningful()
    {
        await using var context = await CreateContextAsync();
        context.Sellers.Add(new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Ada",
            LastName = "Lovelace",
            Phone = "01 02 03"
        });
        await context.SaveChangesAsync();

        var service = new SellerDuplicateService(context);

        var trimmedPhoneMatches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Ada", "Lovelace", " 01 02 03 "));
        var compactPhoneMatches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Ada", "Lovelace", "010203"));

        Assert.Single(trimmedPhoneMatches);
        Assert.Empty(compactPhoneMatches);
    }

    [Fact]
    public async Task FindDuplicateSellersAsync_BlankSubmittedPhoneBlocksSameNameEvenWhenExistingPhoneIsPresent()
    {
        await using var context = await CreateContextAsync();
        context.Sellers.Add(new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Ada",
            LastName = "Lovelace",
            Phone = "0123"
        });
        await context.SaveChangesAsync();

        var service = new SellerDuplicateService(context);

        var matches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Ada", "Lovelace", ""));

        var match = Assert.Single(matches);
        Assert.Equal(SellerDuplicateMatchReason.NameOnlySubmittedPhoneMissing, match.Reason);
    }

    [Fact]
    public async Task FindDuplicateSellersAsync_ExcludesCurrentSellerAndOrdersMatchesBySellerCode()
    {
        await using var context = await CreateContextAsync();
        context.Sellers.AddRange(
            new SellerEntity { SellerCode = "CA", FirstName = "Ada", LastName = "Lovelace" },
            new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" },
            new SellerEntity { SellerCode = "BA", FirstName = "Ada", LastName = "Lovelace" });
        await context.SaveChangesAsync();
        var excludedSeller = await context.Sellers.SingleAsync(x => x.SellerCode == "BA");

        var service = new SellerDuplicateService(context);

        var matches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Ada", "Lovelace", null, excludedSeller.Id));

        Assert.Collection(
            matches,
            first => Assert.Equal("AA", first.SellerCode),
            second => Assert.Equal("CA", second.SellerCode));
    }

    [Fact]
    public async Task FindDuplicateSellersAsync_ReturnsItemCountsAndDisplayContext()
    {
        await using var context = await CreateContextAsync();
        var seller = new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Phone = "0123",
            Items =
            [
                new ItemEntity { Reference = "AA-001", ItemNumber = 1, Title = "Toy", SellerPriceCents = 100, Status = "registered" },
                new ItemEntity { Reference = "AA-002", ItemNumber = 2, Title = "Puzzle", SellerPriceCents = 200, Status = "registered" }
            ]
        };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var service = new SellerDuplicateService(context);

        var match = Assert.Single(await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Ada", "Lovelace", "0123")));

        Assert.Equal(seller.Id, match.SellerId);
        Assert.Equal("Ada", match.FirstName);
        Assert.Equal("Lovelace", match.LastName);
        Assert.Equal("ada@example.com", match.Email);
        Assert.Equal("0123", match.Phone);
        Assert.Equal(2, match.ItemCount);
    }

    private static async Task<AppDbContext> CreateContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
