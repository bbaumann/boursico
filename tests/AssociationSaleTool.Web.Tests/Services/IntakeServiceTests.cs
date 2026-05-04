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

        var firstSeller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", "ada@example.com", "0123", "1 rue des Tests", null));
        var secondSeller = await service.CreateSellerAsync(new CreateSellerRequest("Grace", "Hopper", null, "0456", "2 rue des Tests", null));

        Assert.Equal("AA", firstSeller.SellerCode);
        Assert.Equal("BA", secondSeller.SellerCode);
    }

    [Fact]
    public async Task CreateSellerAsync_RejectsNewSellersWhenCatalogIsLocked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var phaseService = new SalePhaseService(context);
        await phaseService.SetPhaseAsync(SalePhase.Sale);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null)));

        Assert.Equal("Seller master data is locked outside presale.", exception.Message);
        Assert.Empty(await context.Sellers.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateSellerAsync_RejectsBlankPhone(string? phone)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, phone, "1 rue des Tests", null)));

        Assert.Equal("Phone", exception.ParamName);
        Assert.Empty(await context.Sellers.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateSellerAsync_RejectsBlankPostalAddress(string? postalAddress)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", postalAddress, null)));

        Assert.Equal("PostalAddress", exception.ParamName);
        Assert.Empty(await context.Sellers.ToListAsync());
    }


    [Fact]
    public async Task CreateSellerAsync_ReturnsDuplicateResultAndDoesNotCreateSellerForSameNameAndPhone()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var existing = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null))).Seller!;

        var result = await service.CreateSellerAsync(new CreateSellerRequest(" ada ", "lovelace", null, "0123", "2 rue des Tests", null));

        Assert.Equal(CreateSellerOutcome.DuplicateFound, result.Outcome);
        Assert.Null(result.Seller);
        var match = Assert.Single(result.DuplicateMatches);
        Assert.Equal(existing.Id, match.SellerId);
        Assert.Equal(SellerDuplicateMatchReason.NameAndPhone, match.Reason);
        Assert.Equal(1, await context.Sellers.CountAsync());
    }

    [Fact]
    public async Task CreateSellerAsync_RejectsBlankPhoneBeforeDuplicateCheck()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, "1 rue des Tests", null)));

        Assert.Equal("Phone", exception.ParamName);
        Assert.Equal(1, await context.Sellers.CountAsync());
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
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var createdItems = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [
                new CreateItemRowRequest("Wooden train", 500),
                new CreateItemRowRequest("Puzzle box", 600)
            ]));

        Assert.Collection(
            createdItems.OrderBy(item => item.ItemNumber),
            first =>
            {
                Assert.Equal(1, first.ItemNumber);
                Assert.Equal("AA-001", first.Reference);
                Assert.Equal(500, first.SellerPriceCents);
                Assert.Equal("in_stock", first.Status);
            },
            second =>
            {
                Assert.Equal(2, second.ItemNumber);
                Assert.Equal("AA-002", second.Reference);
                Assert.Equal(600, second.SellerPriceCents);
                Assert.Equal("in_stock", second.Status);
            });
    }

    [Fact]
    public async Task CreateItemBatchAsync_RejectsSellerPricesWithCents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateItemBatchAsync(new CreateItemBatchRequest(
                seller.Id,
                IntakeMode.InPerson,
                [new CreateItemRowRequest("Toy piano", 450)])));

        Assert.Contains("whole euro", exception.Message);
        Assert.Empty(await context.Items.ToListAsync());
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
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var createdItems = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));

        Assert.Single(createdItems);
        Assert.Equal("registered", createdItems[0].Status);
    }

    [Fact]
    public async Task CreateItemBatchAsync_RejectsNewItemsWhenCatalogIsLocked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var phaseService = new SalePhaseService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        await phaseService.SetPhaseAsync(SalePhase.Sale);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateItemBatchAsync(new CreateItemBatchRequest(
                seller.Id,
                IntakeMode.InPerson,
                [new CreateItemRowRequest("Toy piano", 1000)])));

        Assert.Equal("Item master data is locked outside presale.", exception.Message);
        Assert.Empty(await context.Items.ToListAsync());
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
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var result = await service.UpdateSellerAsync(seller.Id, new UpdateSellerRequest("Augusta", "King", "augusta@example.com", "1234", "10 avenue des Jouets", "Preferred contact by mail"));

        Assert.Equal(UpdateSellerResult.Updated, result);
        var updatedSeller = await context.Sellers.SingleAsync();
        Assert.Equal("Augusta", updatedSeller.FirstName);
        Assert.Equal("King", updatedSeller.LastName);
        Assert.Equal("augusta@example.com", updatedSeller.Email);
        Assert.Equal("1234", updatedSeller.Phone);
        Assert.Equal("10 avenue des Jouets", updatedSeller.PostalAddress);
        Assert.Equal("Preferred contact by mail", updatedSeller.Notes);
        Assert.Equal("AA", updatedSeller.SellerCode);
    }

    [Fact]
    public async Task UpdateSellerAsync_ReturnsDuplicateResultAndDoesNotUpdateSeller()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var first = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null))).Seller!;
        var second = (await service.CreateSellerAsync(new CreateSellerRequest("Grace", "Hopper", null, "9999", "2 rue des Tests", null))).Seller!;

        var result = await service.UpdateSellerAsync(second.Id, new UpdateSellerRequest("Ada", "Lovelace", null, "0123", "3 rue des Tests", "Updated"));

        Assert.Equal(UpdateSellerOutcome.DuplicateFound, result.Outcome);
        Assert.Equal(first.Id, Assert.Single(result.DuplicateMatches).SellerId);
        var unchangedSecond = await context.Sellers.SingleAsync(x => x.Id == second.Id);
        Assert.Equal("Grace", unchangedSecond.FirstName);
        Assert.Equal("Hopper", unchangedSecond.LastName);
        Assert.Equal("9999", unchangedSecond.Phone);
    }

    [Fact]
    public async Task UpdateSellerAsync_ExcludesCurrentSellerFromDuplicateCheck()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null))).Seller!;

        var result = await service.UpdateSellerAsync(seller.Id, new UpdateSellerRequest("Ada", "Lovelace", "ada@example.com", "0123", "10 avenue des Jouets", "Updated"));

        Assert.Equal(UpdateSellerOutcome.Updated, result.Outcome);
        Assert.Empty(result.DuplicateMatches);
        Assert.Equal("ada@example.com", (await context.Sellers.SingleAsync()).Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateSellerAsync_RejectsBlankPhone(string? phone)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null))).Seller!;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSellerAsync(seller.Id, new UpdateSellerRequest("Ada", "Lovelace", null, phone, "10 avenue des Jouets", "Updated")));

        Assert.Equal("Phone", exception.ParamName);
        Assert.Equal("0123", (await context.Sellers.SingleAsync()).Phone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateSellerAsync_RejectsBlankPostalAddress(string? postalAddress)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null))).Seller!;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateSellerAsync(seller.Id, new UpdateSellerRequest("Ada", "Lovelace", null, "0123", postalAddress, "Updated")));

        Assert.Equal("PostalAddress", exception.ParamName);
        Assert.Equal("1 rue des Tests", (await context.Sellers.SingleAsync()).PostalAddress);
    }


    [Theory]
    [InlineData(SalePhase.Sale)]
    [InlineData(SalePhase.PostSale)]
    public async Task UpdateSellerAsync_RejectsSellerMasterDataChangesWhenCatalogIsLocked(SalePhase phase)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var phaseService = new SalePhaseService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        await phaseService.SetPhaseAsync(phase);

        var result = await service.UpdateSellerAsync(
            seller.Id,
            new UpdateSellerRequest("Augusta", "King", "augusta@example.com", "1234", "10 avenue des Jouets", "Preferred contact by mail"));

        Assert.Equal(UpdateSellerResult.NotAllowed, result);
        var unchangedSeller = await context.Sellers.SingleAsync();
        Assert.Equal("Ada", unchangedSeller.FirstName);
        Assert.Equal("Lovelace", unchangedSeller.LastName);
        Assert.Null(unchangedSeller.Email);
        Assert.Equal("0123", unchangedSeller.Phone);
        Assert.Equal("1 rue des Tests", unchangedSeller.PostalAddress);
        Assert.Null(unchangedSeller.Notes);
    }

    [Fact]
    public async Task UpdateItemAsync_ChangesTitleAndSellerPrice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        await service.UpdateItemAsync(item.Id, new UpdateItemRequest("Large toy piano", 1200));

        var updatedItem = await context.Items.SingleAsync();
        Assert.Equal("Large toy piano", updatedItem.Title);
        Assert.Equal(1200, updatedItem.SellerPriceCents);
        Assert.Equal("AA-001", updatedItem.Reference);
        Assert.Equal("in_stock", updatedItem.Status);
    }

    [Fact]
    public async Task UpdateItemAsync_RejectsSellerPricesWithCents()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateItemAsync(item.Id, new UpdateItemRequest("Large toy piano", 1250)));

        Assert.Contains("whole euro", exception.Message);
        var unchangedItem = await context.Items.SingleAsync();
        Assert.Equal("Toy piano", unchangedItem.Title);
        Assert.Equal(1000, unchangedItem.SellerPriceCents);
    }

    [Theory]
    [InlineData(SalePhase.Sale)]
    [InlineData(SalePhase.PostSale)]
    public async Task UpdateItemAsync_RejectsItemMasterDataChangesWhenCatalogIsLocked(SalePhase phase)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var phaseService = new SalePhaseService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();
        await phaseService.SetPhaseAsync(phase);

        var result = await service.UpdateItemAsync(item.Id, new UpdateItemRequest("Large toy piano", 1200));

        Assert.Equal(UpdateItemResult.NotAllowed, result);
        var unchangedItem = await context.Items.SingleAsync();
        Assert.Equal("Toy piano", unchangedItem.Title);
        Assert.Equal(1000, unchangedItem.SellerPriceCents);
    }

    [Fact]
    public async Task UpdateItemAsync_RejectsSoldItemsEvenInPresale()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-001",
            ItemNumber = 1,
            Title = "Toy piano",
            SellerPriceCents = 1000,
            Status = "sold"
        };
        context.Items.Add(item);
        await context.SaveChangesAsync();

        var result = await service.UpdateItemAsync(item.Id, new UpdateItemRequest("Large toy piano", 1200));

        Assert.Equal(UpdateItemResult.NotAllowed, result);
        var unchangedItem = await context.Items.SingleAsync();
        Assert.Equal("Toy piano", unchangedItem.Title);
        Assert.Equal(1000, unchangedItem.SellerPriceCents);
    }

    [Theory]
    [InlineData("registered")]
    [InlineData("in_stock")]
    public async Task DeleteSellerItemAsync_RemovesItemsInDeletableStatuses(string status)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-001",
            ItemNumber = 1,
            Title = "Toy piano",
            SellerPriceCents = 1000,
            Status = status
        };

        context.Items.Add(item);
        await context.SaveChangesAsync();

        var result = await service.DeleteSellerItemAsync(seller.Id, item.Id);

        Assert.Equal(DeleteItemResult.Deleted, result);
        Assert.Empty(await context.Items.ToListAsync());
    }

    [Theory]
    [InlineData("sold")]
    [InlineData("missing")]
    public async Task DeleteSellerItemAsync_RejectsItemsOutsideDeletableStatuses(string status)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-001",
            ItemNumber = 1,
            Title = "Toy piano",
            SellerPriceCents = 1000,
            Status = status
        };

        context.Items.Add(item);
        await context.SaveChangesAsync();

        var result = await service.DeleteSellerItemAsync(seller.Id, item.Id);

        Assert.Equal(DeleteItemResult.NotAllowed, result);
        Assert.Equal(status, (await context.Items.SingleAsync()).Status);
    }

    [Fact]
    public async Task DeleteSellerItemAsync_RejectsDeletableStatusWhenCatalogIsLocked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var phaseService = new SalePhaseService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();
        await phaseService.SetPhaseAsync(SalePhase.Sale);

        var result = await service.DeleteSellerItemAsync(seller.Id, item.Id);

        Assert.Equal(DeleteItemResult.NotAllowed, result);
        Assert.Equal("in_stock", (await context.Items.SingleAsync()).Status);
    }

    [Fact]
    public async Task DeleteSellerItemAsync_RejectsItemsWithHistoricalSaleLines()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-001",
            ItemNumber = 1,
            Title = "Toy piano",
            SellerPriceCents = 1000,
            Status = "in_stock"
        };
        var sale = new SaleEntity
        {
            SubtotalSellerPriceCents = 1000,
            CheckoutFeeCents = 100,
            TotalPaidCents = 1100,
            SoldAt = DateTime.UtcNow,
            SaleLines =
            [
                new SaleLineEntity
                {
                    Item = item,
                    SellerPriceAtSaleCents = 1000,
                    CorrectedAt = DateTime.UtcNow
                }
            ]
        };
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var result = await service.DeleteSellerItemAsync(seller.Id, item.Id);

        Assert.Equal(DeleteItemResult.NotAllowed, result);
        Assert.Equal("in_stock", (await context.Items.SingleAsync()).Status);
    }

    [Fact]
    public async Task MarkSellerItemInStockAsync_ChangesRegisteredItemToInStock()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = (await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

        var result = await service.MarkSellerItemInStockAsync(seller.Id, item.Id);

        Assert.Equal(ItemStatusChangeResult.Changed, result);
        Assert.Equal("in_stock", (await context.Items.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("in_stock")]
    [InlineData("sold")]
    [InlineData("missing")]
    public async Task MarkSellerItemInStockAsync_RejectsItemsThatAreNotRegistered(string status)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));
        var item = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "AA-001",
            ItemNumber = 1,
            Title = "Toy piano",
            SellerPriceCents = 1000,
            Status = status
        };

        context.Items.Add(item);
        await context.SaveChangesAsync();

        var result = await service.MarkSellerItemInStockAsync(seller.Id, item.Id);

        Assert.Equal(ItemStatusChangeResult.NotAllowed, result);
        Assert.Equal(status, (await context.Items.SingleAsync()).Status);
    }
}


