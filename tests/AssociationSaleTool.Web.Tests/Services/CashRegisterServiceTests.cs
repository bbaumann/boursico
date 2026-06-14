using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class CashRegisterServiceTests
{
    [Fact]
    public async Task SaveCashRegisterAsync_CreatesTrimmedCashRegisterWithInitialCash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new CashRegisterService(context);

        var result = await service.SaveCashRegisterAsync(null, "  Caisse 1  ", 5000, isActive: true);

        Assert.Equal(CashRegisterSaveResult.Saved, result);
        var cashRegister = await context.CashRegisters.SingleAsync();
        Assert.Equal("Caisse 1", cashRegister.Name);
        Assert.Equal(5000, cashRegister.InitialCashCents);
        Assert.True(cashRegister.IsActive);
    }

    [Fact]
    public async Task ListActiveCashRegistersAsync_ReturnsOnlyActiveRowsOrderedByDisplayOrderAndName()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        context.CashRegisters.AddRange(
            new CashRegisterEntity { Name = "Caisse B", InitialCashCents = 5000, IsActive = true, DisplayOrder = 2 },
            new CashRegisterEntity { Name = "Caisse A", InitialCashCents = 4000, IsActive = true, DisplayOrder = 1 },
            new CashRegisterEntity { Name = "Caisse fermée", InitialCashCents = 3000, IsActive = false, DisplayOrder = 0 });
        await context.SaveChangesAsync();
        var service = new CashRegisterService(context);

        var rows = await service.ListActiveCashRegistersAsync();

        Assert.Equal(["Caisse A", "Caisse B"], rows.Select(row => row.Name));
        Assert.Equal([4000, 5000], rows.Select(row => row.InitialCashCents));
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
