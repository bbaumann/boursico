using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class IntakeServiceTests
{
    [Fact]
    public async Task CreateSellerAsync_AssignsNextSellerCode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);

        var firstSeller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", "ada@example.com", null, null));
        var secondSeller = await service.CreateSellerAsync(new CreateSellerRequest("Grace", "Hopper", null, null, null));

        Assert.Equal("AA", firstSeller.SellerCode);
        Assert.Equal("BA", secondSeller.SellerCode);
    }

    [Fact]
    public async Task CreateItemBatchAsync_CreatesMultipleItemsForSellerWithSequentialReferences()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));

        var createdItems = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [
                new CreateItemRowRequest("Wooden train", 500),
                new CreateItemRowRequest("Puzzle box", 650)
            ]));

        Assert.Collection(
            createdItems.OrderBy(item => item.ItemNumber),
            first =>
            {
                Assert.Equal(1, first.ItemNumber);
                Assert.Equal("AA-001", first.Reference);
                Assert.Equal(500, first.SellerPriceCents);
                Assert.Equal(600, first.SalePriceCents);
                Assert.Equal("in_stock", first.Status);
            },
            second =>
            {
                Assert.Equal(2, second.ItemNumber);
                Assert.Equal("AA-002", second.Reference);
                Assert.Equal(650, second.SellerPriceCents);
                Assert.Equal(780, second.SalePriceCents);
                Assert.Equal("in_stock", second.Status);
            });
    }

    [Fact]
    public async Task CreateItemBatchAsync_UsesRegisteredStatusForEmailPreregistration()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));

        var createdItems = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));

        Assert.Single(createdItems);
        Assert.Equal("registered", createdItems[0].Status);
    }

    [Fact]
    public async Task UpdateSellerAsync_ChangesEditableFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));

        await service.UpdateSellerAsync(seller.Id, new UpdateSellerRequest("Augusta", "King", "augusta@example.com", "1234", "Preferred contact by mail"));

        var updatedSeller = await context.Sellers.SingleAsync();
        Assert.Equal("Augusta", updatedSeller.FirstName);
        Assert.Equal("King", updatedSeller.LastName);
        Assert.Equal("augusta@example.com", updatedSeller.Email);
        Assert.Equal("1234", updatedSeller.Phone);
        Assert.Equal("Preferred contact by mail", updatedSeller.Notes);
        Assert.Equal("AA", updatedSeller.SellerCode);
    }

    [Fact]
    public async Task UpdateItemAsync_ChangesTitleAndRecomputesSalePriceFromSellerPrice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        await service.UpdateItemAsync(item.Id, new UpdateItemRequest("Large toy piano", 1200));

        var updatedItem = await context.Items.SingleAsync();
        Assert.Equal("Large toy piano", updatedItem.Title);
        Assert.Equal(1200, updatedItem.SellerPriceCents);
        Assert.Equal(1440, updatedItem.SalePriceCents);
        Assert.Equal("AA-001", updatedItem.Reference);
        Assert.Equal("in_stock", updatedItem.Status);
    }
}
