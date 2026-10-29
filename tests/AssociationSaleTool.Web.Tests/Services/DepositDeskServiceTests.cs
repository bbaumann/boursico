using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class DepositDeskServiceTests
{
    private static async Task<AppDbContext> CreateContextAsync(SqliteConnection connection)
    {
        await connection.OpenAsync();
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task OpenDeskAsync_GivesEachDeskTwoDistinctLeastUsedLetters()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);

        var first = await service.OpenDeskAsync();
        var second = await service.OpenDeskAsync();

        Assert.Equal("Poste 1", first.Name);
        Assert.Equal(['B', 'C'], first.Letters);
        Assert.Equal("Poste 2", second.Name);
        Assert.Equal(['D', 'E'], second.Letters);
        Assert.NotEqual(first.Token, second.Token);
    }

    [Fact]
    public async Task OpenDeskAsync_UsesLettersPerDeskFromOptions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context, Options.Create(new LabelsOptions { LettersPerDesk = 3 }));

        var desk = await service.OpenDeskAsync();

        Assert.Equal(['B', 'C', 'D'], desk.Letters);
    }

    [Fact]
    public async Task OpenDeskAsync_OpensWithoutLettersWhenNoneIsFree()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        for (var i = 0; i < 10; i++)
        {
            await service.OpenDeskAsync();
        }

        var eleventh = await service.OpenDeskAsync();

        Assert.Empty(eleventh.Letters);
        Assert.Equal(DeskState.Open, (await service.GetCurrentDeskAsync(eleventh.Token)).State);
    }

    [Fact]
    public async Task CloseDeskAsync_FreesTheLettersAndMarksTheDeskClosed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        var first = await service.OpenDeskAsync();

        Assert.True(await service.CloseDeskAsync(first.DeskId));
        var second = await service.OpenDeskAsync();

        Assert.Equal(DeskState.Closed, (await service.GetCurrentDeskAsync(first.Token)).State);
        Assert.Equal(['B', 'C'], second.Letters);
        Assert.False(await service.CloseDeskAsync(first.DeskId));
    }

    [Fact]
    public async Task GetCurrentDeskAsync_ReportsNoneForAMissingOrUnknownToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);

        Assert.Equal(DeskState.None, (await service.GetCurrentDeskAsync(null)).State);
        Assert.Equal(DeskState.None, (await service.GetCurrentDeskAsync("inconnu")).State);
    }

    [Fact]
    public async Task GetCurrentDeskAsync_ReportsLabelsLeftPerHeldLetter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        var desk = await service.OpenDeskAsync();
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "B")).LastNumber = 440;
        await context.SaveChangesAsync();

        var current = await service.GetCurrentDeskAsync(desk.Token);

        Assert.Equal([new DeskLetterInfo('B', 15), new DeskLetterInfo('C', 455)], current.Letters);
    }

    [Fact]
    public async Task SetLabelsPerLetterAsync_RefusesOutOfRangeValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);

        Assert.Equal(LabelsPerLetterUpdateStatus.OutOfRange, (await service.SetLabelsPerLetterAsync(0)).Status);
        Assert.Equal(LabelsPerLetterUpdateStatus.OutOfRange, (await service.SetLabelsPerLetterAsync(1000)).Status);
        Assert.Equal(455, await service.GetLabelsPerLetterAsync());
    }

    [Fact]
    public async Task SetLabelsPerLetterAsync_RefusesAValueBelowANumberAlreadyUsed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "K")).LastNumber = 15;
        await context.SaveChangesAsync();

        var result = await service.SetLabelsPerLetterAsync(10);

        Assert.Equal(new LabelsPerLetterUpdateResult(LabelsPerLetterUpdateStatus.BelowUsed, 'K', 15), result);
        Assert.Equal(455, await service.GetLabelsPerLetterAsync());
    }

    [Fact]
    public async Task SetLabelsPerLetterAsync_RaisingTheValueMakesAnExhaustedLetterTakeableAgain()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        await service.SetLabelsPerLetterAsync(20);
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "B")).LastNumber = 20;
        await context.SaveChangesAsync();
        Assert.Equal(['C', 'D'], (await service.OpenDeskAsync()).Letters);

        Assert.Equal(LabelsPerLetterUpdateStatus.Saved, (await service.SetLabelsPerLetterAsync(30)).Status);
        // A desk asking for every free letter shows B is takeable again (a
        // default 2-letter desk would pick unused letters first).
        var next = await new DepositDeskService(context, Options.Create(new LabelsOptions { LettersPerDesk = 20 }))
            .OpenDeskAsync();

        Assert.Contains('B', next.Letters);
    }

    [Fact]
    public async Task ListOpenDesksAsync_ListsOnlyOpenDesksWithTheirLetters()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        var closed = await service.OpenDeskAsync();
        await service.OpenDeskAsync();
        await service.CloseDeskAsync(closed.DeskId);

        var desk = Assert.Single(await service.ListOpenDesksAsync());

        Assert.Equal("Poste 2", desk.Name);
        Assert.Equal(['D', 'E'], desk.Letters);
    }

    [Fact]
    public async Task ListLettersAsync_ShowsUsageAndHolder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        await service.OpenDeskAsync();
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "D")).LastNumber = 455;
        await context.SaveChangesAsync();

        var letters = await service.ListLettersAsync();

        Assert.Equal(20, letters.Count);
        Assert.Equal(new ReferenceLetterRow('B', 0, 455, "Poste 1"), letters[0]);
        Assert.Equal(new ReferenceLetterRow('D', 455, 0, null), letters[2]);
    }

    [Fact]
    public async Task OpenDeskAsync_LeavesNoDeskRowWhenALetterRaceIsLost()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        await using var rival = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var service = new DepositDeskService(context);

        // Both contexts see the same free letters; the rival saves first, so
        // the loser's tracked letters are stale and its save must be refused.
        await context.ReferenceLetters.ToListAsync();
        await new DepositDeskService(rival).OpenDeskAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.OpenDeskAsync());

        var names = await rival.DepositDesks.AsNoTracking().Select(x => x.Name).ToListAsync();
        Assert.Equal(["Poste 1"], names);
    }
}
