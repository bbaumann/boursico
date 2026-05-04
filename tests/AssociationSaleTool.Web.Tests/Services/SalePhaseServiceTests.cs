using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class SalePhaseServiceTests
{
    [Fact]
    public async Task GetCurrentPhaseAsync_DefaultsToPresale()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new SalePhaseService(context);

        var phase = await service.GetCurrentPhaseAsync();

        Assert.Equal(SalePhase.Presale, phase);
    }

    [Fact]
    public async Task SetPhaseAsync_PersistsPhase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new SalePhaseService(context);

        await service.SetPhaseAsync(SalePhase.PostSale);

        Assert.Equal(SalePhase.PostSale, await service.GetCurrentPhaseAsync());
    }

    [Theory]
    [InlineData(SalePhase.Presale, true)]
    [InlineData(SalePhase.Sale, true)]
    [InlineData(SalePhase.PostSale, false)]
    public async Task CanCreateSalesAsync_AllowsSalesUntilPostSale(SalePhase phase, bool expected)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new SalePhaseService(context);
        await service.SetPhaseAsync(phase);

        var canCreateSales = await service.CanCreateSalesAsync();

        Assert.Equal(expected, canCreateSales);
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
