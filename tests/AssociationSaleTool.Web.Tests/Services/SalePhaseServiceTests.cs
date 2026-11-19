using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
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

    [Fact]
    public async Task ListUnfinishedDepositsAsync_ListsOnlyStartedDepositsNotYetValidated()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var startedB = new SellerEntity { SellerCode = "BB", FirstName = "Berthe", LastName = "Commencee", DepositStatus = "deposit_pending", DepositStartedAt = DateTime.UtcNow };
        var startedA = new SellerEntity { SellerCode = "AA", FirstName = "Alain", LastName = "Commence", DepositStatus = "deposit_pending", DepositStartedAt = DateTime.UtcNow };
        context.Sellers.AddRange(
            startedB,
            startedA,
            new SellerEntity { SellerCode = "CC", FirstName = "Chloe", LastName = "Absente", DepositStatus = "deposit_pending" },
            new SellerEntity { SellerCode = "DD", FirstName = "Denis", LastName = "Valide", DepositStatus = "deposit_validated", DepositStartedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var service = new SalePhaseService(context);

        var deposits = await service.ListUnfinishedDepositsAsync();

        Assert.Equal(
            [
                new UnfinishedDeposit(startedA.Id, "AA", "Alain Commence"),
                new UnfinishedDeposit(startedB.Id, "BB", "Berthe Commencee")
            ],
            deposits);
    }

    [Fact]
    public async Task ListUnfinishedDepositsAsync_IsEmptyWhenEveryStartedDepositIsValidated()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        context.Sellers.Add(new SellerEntity { SellerCode = "DD", FirstName = "Denis", LastName = "Valide", DepositStatus = "deposit_validated", DepositStartedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var service = new SalePhaseService(context);

        Assert.Empty(await service.ListUnfinishedDepositsAsync());
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
