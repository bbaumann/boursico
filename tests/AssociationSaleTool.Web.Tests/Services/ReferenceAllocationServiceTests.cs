using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class ReferenceAllocationServiceTests
{
    private static async Task<AppDbContext> CreateContextAsync(SqliteConnection connection)
    {
        await connection.OpenAsync();
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static async Task<SellerEntity> SeedSellerAsync(AppDbContext context, int itemCount, string sellerCode = "BB")
    {
        var seller = new SellerEntity { SellerCode = sellerCode, FirstName = "Ada", LastName = "Lovelace" };
        for (var i = 0; i < itemCount; i++)
        {
            seller.Items.Add(new ItemEntity { Title = $"Jouet {i + 1}", SellerPriceCents = 100, Status = "registered" });
        }

        context.Sellers.Add(seller);
        await context.SaveChangesAsync();
        return seller;
    }

    [Fact]
    public async Task AllocateAsync_GivesARunOfConsecutiveNumbersFromTheDeskLetter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desk = await new DepositDeskService(context).OpenDeskAsync();
        var seller = await SeedSellerAsync(context, 3);

        var result = await new ReferenceAllocationService(context).AllocateAsync(desk.DeskId, null, seller.Items);
        await context.SaveChangesAsync();

        Assert.Equal(ReferenceAllocationStatus.Allocated, result.Status);
        Assert.Equal(["B001", "B002", "B003"], result.AssignedReferences);
        Assert.Equal(["B001", "B002", "B003"], seller.Items.Select(x => x.Reference));
        Assert.Equal(3, (await context.ReferenceLetters.SingleAsync(x => x.Letter == "B")).LastNumber);
    }

    [Fact]
    public async Task AllocateAsync_NeverReusesTheNumberOfADeletedItem()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desk = await new DepositDeskService(context).OpenDeskAsync();
        var seller = await SeedSellerAsync(context, 2);
        var service = new ReferenceAllocationService(context);
        await service.AllocateAsync(desk.DeskId, null, seller.Items);
        await context.SaveChangesAsync();

        context.Items.Remove(seller.Items.Single(x => x.Reference == "B002"));
        await context.SaveChangesAsync();
        var extra = new ItemEntity { SellerId = seller.Id, Title = "Extra", SellerPriceCents = 100, Status = "registered" };
        context.Items.Add(extra);
        var result = await service.AllocateAsync(desk.DeskId, 'B', [extra]);
        await context.SaveChangesAsync();

        Assert.Equal(["B003"], result.AssignedReferences);
    }

    [Fact]
    public async Task AllocateAsync_RefusesWithoutADeskOrOnAClosedDeskAndChangesNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desks = new DepositDeskService(context);
        var desk = await desks.OpenDeskAsync();
        await desks.CloseDeskAsync(desk.DeskId);
        var seller = await SeedSellerAsync(context, 1);
        var service = new ReferenceAllocationService(context);

        Assert.Equal(ReferenceAllocationStatus.NoDesk, (await service.AllocateAsync(null, null, seller.Items)).Status);
        Assert.Equal(ReferenceAllocationStatus.DeskClosed, (await service.AllocateAsync(desk.DeskId, null, seller.Items)).Status);
        Assert.Null(seller.Items[0].Reference);
        Assert.All(await context.ReferenceLetters.ToListAsync(), x => Assert.Equal(0, x.LastNumber));
    }

    [Fact]
    public async Task AllocateAsync_ReleasesAnExhaustedLetterAndTakesAReplacement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desks = new DepositDeskService(context);
        await desks.SetLabelsPerLetterAsync(20);
        var desk = await desks.OpenDeskAsync();
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "B")).LastNumber = 18;
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "C")).LastNumber = 17;
        await context.SaveChangesAsync();
        var seller = await SeedSellerAsync(context, 8);

        var result = await new ReferenceAllocationService(context).AllocateAsync(desk.DeskId, 'B', seller.Items);
        await context.SaveChangesAsync();

        Assert.Equal(
            ["B019", "B020", "C018", "C019", "C020", "D001", "D002", "D003"],
            result.AssignedReferences);
        // D was taken mid-run; B and C are exhausted and released; E tops the desk back up to two letters.
        Assert.Equal(['D', 'E'], result.TakenLetters);
        var held = await context.ReferenceLetters.Where(x => x.HeldByDeskId == desk.DeskId).Select(x => x.Letter).ToListAsync();
        Assert.Equal(["D", "E"], held.Order());
    }

    [Fact]
    public async Task AllocateAsync_RefusesWhenNoLetterHasRoomAndChangesNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desks = new DepositDeskService(context);
        await desks.SetLabelsPerLetterAsync(1);
        var desk = await desks.OpenDeskAsync();
        foreach (var letter in await context.ReferenceLetters.ToListAsync())
        {
            letter.LastNumber = 1;
        }

        await context.SaveChangesAsync();
        var seller = await SeedSellerAsync(context, 1);

        var result = await new ReferenceAllocationService(context).AllocateAsync(desk.DeskId, null, seller.Items);

        Assert.Equal(ReferenceAllocationStatus.NoLabelsLeft, result.Status);
        Assert.Null(seller.Items[0].Reference);
    }

    [Fact]
    public async Task AllocateAsync_WithNoItemsAllocatesNothingButRecordsActivity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desk = await new DepositDeskService(context).OpenDeskAsync();
        var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        (await context.DepositDesks.SingleAsync()).LastActivityAt = old;
        await context.SaveChangesAsync();

        var result = await new ReferenceAllocationService(context).AllocateAsync(desk.DeskId, null, []);
        await context.SaveChangesAsync();

        Assert.Equal(ReferenceAllocationStatus.Allocated, result.Status);
        Assert.Empty(result.AssignedReferences);
        // The desk already holds its two letters, so nothing is topped up.
        Assert.Empty(result.TakenLetters);
        Assert.True((await context.DepositDesks.SingleAsync()).LastActivityAt > old);
    }

    [Fact]
    public async Task AllocateAsync_ConcurrentTakeOfTheSameLetterFailsForTheSecondDesk()
    {
        // Two contexts on one database model two desks reading the same snapshot.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var first = await CreateContextAsync(connection);
        await using var second = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        // Desks without letters, so both runs must take the first free letter, B.
        var options = Options.Create(new LabelsOptions { LettersPerDesk = 0 });
        var deskA = await new DepositDeskService(first, options).OpenDeskAsync();
        var deskB = await new DepositDeskService(first, options).OpenDeskAsync();
        var sellerA = await SeedSellerAsync(first, 1);
        var sellerB = await SeedSellerAsync(first, 1, "BC");
        var itemB = await second.Items.SingleAsync(x => x.Id == sellerB.Items[0].Id);

        await new ReferenceAllocationService(first, options).AllocateAsync(deskA.DeskId, null, sellerA.Items);
        await new ReferenceAllocationService(second, options).AllocateAsync(deskB.DeskId, null, [itemB]);

        await first.SaveChangesAsync();
        // Both runs computed B001: the item.reference unique index fires before
        // the reference_letter concurrency token, so any DbUpdateException.
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task AllocateAsync_ConcurrentTopUpOfTheSameFreeLetterFailsOnTheConcurrencyToken()
    {
        // With no items only the letter take collides, which pins the
        // concurrency token on reference_letter.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var first = await CreateContextAsync(connection);
        await using var second = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var noLetters = Options.Create(new LabelsOptions { LettersPerDesk = 0 });
        var oneLetter = Options.Create(new LabelsOptions { LettersPerDesk = 1 });
        var deskA = await new DepositDeskService(first, noLetters).OpenDeskAsync();
        var deskB = await new DepositDeskService(first, noLetters).OpenDeskAsync();

        var resultA = await new ReferenceAllocationService(first, oneLetter).AllocateAsync(deskA.DeskId, null, []);
        var resultB = await new ReferenceAllocationService(second, oneLetter).AllocateAsync(deskB.DeskId, null, []);
        Assert.Equal(['B'], resultA.TakenLetters);
        Assert.Equal(['B'], resultB.TakenLetters);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
