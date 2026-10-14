using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

        Assert.Equal("BB", firstSeller.SellerCode);
        Assert.Equal("CB", secondSeller.SellerCode);
    }

    [Fact]
    public async Task CreateSellerAsync_SkipsExistingGeneratedSellerCode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Sellers.Add(new SellerEntity
        {
            SellerCode = "CB",
            FirstName = "Existing",
            LastName = "Seller",
            Phone = "0101010101",
            PostalAddress = "1 rue des Tests"
        });
        await context.SaveChangesAsync();

        var service = new IntakeService(context);

        var createdSeller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", "ada@example.com", "0123", "2 rue des Tests", null));

        Assert.Equal("BB", createdSeller.SellerCode);
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

    [Fact]
    public async Task CreateSellerAsync_AllowsBlankPendingSellerContactInformation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);

        var result = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, "   ", null));

        Assert.Equal(CreateSellerOutcome.Created, result.Outcome);
        var seller = await context.Sellers.SingleAsync();
        Assert.Equal(string.Empty, seller.Phone);
        Assert.Equal(string.Empty, seller.PostalAddress);
        Assert.Equal(string.Empty, seller.PostalCode);
        Assert.Equal(string.Empty, seller.City);
        Assert.Equal("deposit_pending", seller.DepositStatus);
    }

    [Fact]
    public async Task CreateSellerAsync_AllowsBlankPendingSellerIdentityDocumentInformation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);

        var result = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        Assert.Equal(CreateSellerOutcome.Created, result.Outcome);
        var seller = await context.Sellers.SingleAsync();
        Assert.Null(seller.IdentityDocumentType);
        Assert.Null(seller.IdentityDocumentNumber);
        Assert.Null(seller.IdentityDocumentIssuedOn);
        Assert.Equal("deposit_pending", seller.DepositStatus);
    }

    [Fact]
    public async Task CreateSellerAsync_CanonicalizesKnownIsereCity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context, postalCityLookup: CreateLookup("38120", "Saint-Égrève"));

        var result = await service.CreateSellerAsync(
            new CreateSellerRequest(
                "Ada",
                "Lovelace",
                null,
                "0123",
                "1 rue des Tests",
                null,
                PostalCode: "38120",
                City: "St Egreve"));

        Assert.Equal(CreateSellerOutcome.Created, result.Outcome);
        var seller = await context.Sellers.SingleAsync();
        Assert.Equal("38120", seller.PostalCode);
        Assert.Equal("Saint-Égrève", seller.City);
    }

    [Fact]
    public async Task CreateSellerAsync_RejectsUnknownCityForKnownIserePostalCode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context, postalCityLookup: CreateLookup("38120", "Saint-Égrève"));

        var exception = await Assert.ThrowsAsync<InvalidSellerPostalCityException>(() =>
            service.CreateSellerAsync(
                new CreateSellerRequest(
                    "Ada",
                    "Lovelace",
                    null,
                    "0123",
                    "1 rue des Tests",
                    null,
                    PostalCode: "38120",
                    City: "Grenoble")));

        Assert.Equal("La ville doit correspondre au code postal sélectionné.", exception.Message);
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
    public async Task CreateSellerAsync_ReturnsDuplicateResultForSameNameWhenSubmittedPhoneIsBlank()
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

        var result = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, "1 rue des Tests", null));

        Assert.Equal(CreateSellerOutcome.DuplicateFound, result.Outcome);
        var match = Assert.Single(result.DuplicateMatches);
        Assert.Equal(existing.Id, match.SellerId);
        Assert.Equal(SellerDuplicateMatchReason.NameOnlySubmittedPhoneMissing, match.Reason);
        Assert.Equal(1, await context.Sellers.CountAsync());
    }

    [Fact]
    public async Task CreateSellerWithItemsAsync_CreatesSellerAndRegisteredItemsInOneSave()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);

        var result = await service.CreateSellerWithItemsAsync(
            new CreateSellerWithItemsRequest(
                "Marie",
                "Durand",
                null,
                "0600000000",
                "UNKNOWN",
                null,
                IntakeMode.EmailPreregistration,
                [
                    new CreateItemRowRequest("Puzzle bois", 500),
                    new CreateItemRowRequest("Train", 1200)
                ]));

        Assert.Equal(CreateSellerWithItemsOutcome.Created, result.Outcome);
        var seller = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal("BB", seller.SellerCode);
        Assert.Equal("UNKNOWN", seller.PostalAddress);
        Assert.Equal(0, seller.FeePaidCents);
        Assert.Equal("deposit_pending", seller.DepositStatus);
        Assert.Collection(
            seller.Items.OrderBy(x => x.Id),
            first =>
            {
                Assert.Null(first.Reference);
                Assert.Equal("registered", first.Status);
                Assert.Equal(500, first.SellerPriceCents);
            },
            second =>
            {
                Assert.Null(second.Reference);
                Assert.Equal("registered", second.Status);
                Assert.Equal(1200, second.SellerPriceCents);
            });
    }

    [Fact]
    public async Task CreateSellerWithItemsAsync_AllowsBlankPendingSellerContactInformation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);

        var result = await service.CreateSellerWithItemsAsync(
            new CreateSellerWithItemsRequest(
                "Marie",
                "Durand",
                null,
                null,
                null,
                null,
                IntakeMode.EmailPreregistration,
                [new CreateItemRowRequest("Puzzle bois", 500)]));

        Assert.Equal(CreateSellerWithItemsOutcome.Created, result.Outcome);
        var seller = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal(string.Empty, seller.Phone);
        Assert.Equal(string.Empty, seller.PostalAddress);
        Assert.Equal(string.Empty, seller.PostalCode);
        Assert.Equal(string.Empty, seller.City);
        Assert.Equal("deposit_pending", seller.DepositStatus);
        Assert.Equal("registered", Assert.Single(seller.Items).Status);
    }

    [Fact]
    public async Task CreateSellerWithItemsAsync_CreatesRegisteredItemsForInPersonIntake()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);

        var result = await service.CreateSellerWithItemsAsync(
            new CreateSellerWithItemsRequest(
                "Nadia",
                "Petit",
                null,
                "0611111111",
                "UNKNOWN",
                null,
                IntakeMode.InPerson,
                [new CreateItemRowRequest("Garage", 800)]));

        Assert.Equal(CreateSellerWithItemsOutcome.Created, result.Outcome);
        var item = await context.Items.SingleAsync();
        Assert.Equal("registered", item.Status);
    }

    [Fact]
    public async Task CreateSellerWithItemsAsync_ReturnsExistingSellerAndCreatesNothingForDuplicate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var existing = (await service.CreateSellerAsync(
            new CreateSellerRequest("Marie", "Durand", null, "0600000000", "1 rue des Tests", null))).Seller!;

        var result = await service.CreateSellerWithItemsAsync(
            new CreateSellerWithItemsRequest(
                " marie ",
                "durand",
                null,
                "0600000000",
                "UNKNOWN",
                null,
                IntakeMode.EmailPreregistration,
                [new CreateItemRowRequest("Puzzle bois", 500)]));

        Assert.Equal(CreateSellerWithItemsOutcome.ExistingSellerFound, result.Outcome);
        Assert.Null(result.Seller);
        Assert.Equal(existing.Id, Assert.Single(result.DuplicateMatches).SellerId);
        Assert.Equal(1, await context.Sellers.CountAsync());
        Assert.Empty(await context.Items.ToListAsync());
    }

    [Fact]
    public async Task CreateSellerWithItemsAsync_RejectsEmptyRowsAndCreatesNothing()
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
            service.CreateSellerWithItemsAsync(
                new CreateSellerWithItemsRequest(
                    "Marie",
                    "Durand",
                    null,
                    "0600000000",
                    "UNKNOWN",
                    null,
                    IntakeMode.EmailPreregistration,
                    [])));

        Assert.Equal("Rows", exception.ParamName);
        Assert.Empty(await context.Sellers.ToListAsync());
        Assert.Empty(await context.Items.ToListAsync());
    }

    [Fact]
    public async Task CreateSellerWithItemsAsync_RejectsInvalidItemPriceAndCreatesNothing()
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
            service.CreateSellerWithItemsAsync(
                new CreateSellerWithItemsRequest(
                    "Marie",
                    "Durand",
                    null,
                    "0600000000",
                    "UNKNOWN",
                    null,
                    IntakeMode.EmailPreregistration,
                    [new CreateItemRowRequest("Puzzle bois", 550)])));

        Assert.Contains("whole euro", exception.Message);
        Assert.Empty(await context.Sellers.ToListAsync());
        Assert.Empty(await context.Items.ToListAsync());
    }

    [Fact]
    public async Task CreateItemBatchAsync_CreatesMultipleRegisteredItemsWithoutReferences()
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
            createdItems.Items.OrderBy(item => item.Id),
            first =>
            {
                Assert.Null(first.Reference);
                Assert.Equal(500, first.SellerPriceCents);
                Assert.Equal("registered", first.Status);
            },
            second =>
            {
                Assert.Null(second.Reference);
                Assert.Equal(600, second.SellerPriceCents);
                Assert.Equal("registered", second.Status);
            });
    }

    [Fact]
    public async Task CreateItemBatchAsync_LeavesReferencesUnassignedUntilDeposit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(
            new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var created = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.InPerson,
            [new CreateItemRowRequest("Puzzle", 500), new CreateItemRowRequest("Poupée", 300)]));

        Assert.All(created.Items, item => Assert.Null(item.Reference));
        Assert.All(created.Items, item => Assert.Equal("registered", item.Status));
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

        Assert.Single(createdItems.Items);
        Assert.Equal("registered", createdItems.Items[0].Status);
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
        Assert.Equal("BB", updatedSeller.SellerCode);
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

    [Fact]
    public async Task UpdateSellerAsync_AllowsClearingPendingSellerContactInformation()
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

        var result = await service.UpdateSellerAsync(
            seller.Id,
            new UpdateSellerRequest("Ada", "Lovelace", null, null, "   ", "Updated"));

        Assert.Equal(UpdateSellerResult.Updated, result);
        var updatedSeller = await context.Sellers.SingleAsync();
        Assert.Equal(string.Empty, updatedSeller.Phone);
        Assert.Equal(string.Empty, updatedSeller.PostalAddress);
        Assert.Equal(string.Empty, updatedSeller.PostalCode);
        Assert.Equal(string.Empty, updatedSeller.City);
        Assert.Equal("Updated", updatedSeller.Notes);
    }

    [Fact]
    public async Task UpdateSellerAsync_RejectsUnknownCityForKnownIserePostalCode()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context, postalCityLookup: CreateLookup("38120", "Saint-Égrève"));
        var seller = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null))).Seller!;

        var exception = await Assert.ThrowsAsync<InvalidSellerPostalCityException>(() =>
            service.UpdateSellerAsync(
                seller.Id,
                new UpdateSellerRequest(
                    "Ada",
                    "Lovelace",
                    null,
                    "0123",
                    "1 rue des Tests",
                    null,
                    PostalCode: "38120",
                    City: "Grenoble")));

        Assert.Equal("La ville doit correspondre au code postal sélectionné.", exception.Message);
        var unchangedSeller = await context.Sellers.SingleAsync();
        Assert.Equal(string.Empty, unchangedSeller.PostalCode);
        Assert.Equal(string.Empty, unchangedSeller.City);
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
            [new CreateItemRowRequest("Toy piano", 1000)]))).Items.Single();

        await service.UpdateItemAsync(item.Id, new UpdateItemRequest("Large toy piano", 1200));

        var updatedItem = await context.Items.SingleAsync();
        Assert.Equal("Large toy piano", updatedItem.Title);
        Assert.Equal(1200, updatedItem.SellerPriceCents);
        Assert.Null(updatedItem.Reference);
        Assert.Equal("registered", updatedItem.Status);
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
            [new CreateItemRowRequest("Toy piano", 1000)]))).Items.Single();

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
            [new CreateItemRowRequest("Toy piano", 1000)]))).Items.Single();
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
            [new CreateItemRowRequest("Toy piano", 1000)]))).Items.Single();
        await phaseService.SetPhaseAsync(SalePhase.Sale);

        var result = await service.DeleteSellerItemAsync(seller.Id, item.Id);

        Assert.Equal(DeleteItemResult.NotAllowed, result);
        Assert.Equal("registered", (await context.Items.SingleAsync()).Status);
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
            Title = "Toy piano",
            SellerPriceCents = 1000,
            Status = "in_stock"
        };
        var cashRegister = new CashRegisterEntity
        {
            Name = "CAISSE-1",
            InitialCashCents = 5000,
            IsActive = true
        };
        context.CashRegisters.Add(cashRegister);
        await context.SaveChangesAsync();
        var sale = new SaleEntity
        {
            CashRegisterId = cashRegister.Id,
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
    public async Task ValidateSellerDepositAsync_MovesRegisteredItemsToStockAndLocksSeller()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [
                new CreateItemRowRequest("Toy piano", 1000),
                new CreateItemRowRequest("Wooden train", 1200)
            ]));
        await service.StartDepositAsync(seller.Id, await OpenDeskAsync(context));

        var result = await service.ValidateSellerDepositAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.Changed, result);
        var validatedSeller = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal("deposit_validated", validatedSeller.DepositStatus);
        Assert.All(validatedSeller.Items, item => Assert.Equal("in_stock", item.Status));
    }

    [Fact]
    public async Task ValidateSellerDepositAsync_RejectsSellerWithItemMissingPrice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)));
        context.Items.Add(new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "BB001",
            Title = "Jouet sans prix",
            SellerPriceCents = null,
            Status = "registered"
        });
        await context.SaveChangesAsync();

        var result = await service.ValidateSellerDepositAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.NotAllowed, result);
        var rejectedSeller = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal("deposit_pending", rejectedSeller.DepositStatus);
        Assert.Equal("registered", Assert.Single(rejectedSeller.Items).Status);
    }

    [Fact]
    public async Task ValidateSellerDepositAsync_ToleratesHistoricalItemStatuses()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)));
        context.Items.AddRange(
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-001",
                Title = "Toy piano",
                SellerPriceCents = 1000,
                Status = "registered"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-002",
                Title = "Sold train",
                SellerPriceCents = 1200,
                Status = "sold"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-003",
                Title = "Missing puzzle",
                SellerPriceCents = 800,
                Status = "missing"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-004",
                Title = "Defective game",
                SellerPriceCents = 600,
                Status = "defective"
            });
        await context.SaveChangesAsync();

        var result = await service.ValidateSellerDepositAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.Changed, result);
        var statusesByReference = await context.Items
            .OrderBy(x => x.Reference)
            // Every item seeded above has an explicit Reference, so the key is
            // never null here.
            .ToDictionaryAsync(x => x.Reference!, x => x.Status);
        Assert.Equal("in_stock", statusesByReference["AA-001"]);
        Assert.Equal("sold", statusesByReference["AA-002"]);
        Assert.Equal("missing", statusesByReference["AA-003"]);
        Assert.Equal("defective", statusesByReference["AA-004"]);
        Assert.Equal("deposit_validated", (await context.Sellers.SingleAsync()).DepositStatus);
    }

    [Fact]
    public async Task ValidateSellerDepositAsync_RejectsSellerWithoutItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)));

        var result = await service.ValidateSellerDepositAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.NotAllowed, result);
        Assert.Equal("deposit_pending", (await context.Sellers.SingleAsync()).DepositStatus);
    }

    [Fact]
    public async Task ValidateSellerDepositAsync_RejectsSellerWithIncompleteContactInformation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null, null));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));

        var result = await service.ValidateSellerDepositAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.NotAllowed, result);
        var unchangedSeller = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal("deposit_pending", unchangedSeller.DepositStatus);
        Assert.Equal("registered", Assert.Single(unchangedSeller.Items).Status);
    }

    [Theory]
    [InlineData("identity_document_type")]
    [InlineData("identity_document_number")]
    [InlineData("identity_document_issued_on")]
    public async Task ValidateSellerDepositAsync_RejectsSellerWithIncompleteIdentityDocumentInformation(string missingField)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: missingField == "identity_document_type" ? null : "CNI",
            IdentityDocumentNumber: missingField == "identity_document_number" ? null : "123456789",
            IdentityDocumentIssuedOn: missingField == "identity_document_issued_on" ? null : new DateOnly(2020, 1, 15)));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));

        var result = await service.ValidateSellerDepositAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.NotAllowed, result);
        var unchangedSeller = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal("deposit_pending", unchangedSeller.DepositStatus);
        Assert.Equal("registered", Assert.Single(unchangedSeller.Items).Status);
    }

    [Fact]
    public async Task UpdateSellerAsync_RejectsValidatedSeller()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));
        await service.StartDepositAsync(seller.Id, await OpenDeskAsync(context));
        await service.ValidateSellerDepositAsync(seller.Id);

        var result = await service.UpdateSellerAsync(
            seller.Id,
            new UpdateSellerRequest("Augusta", "King", null, "0456", "2 rue des Tests", null));

        Assert.Equal(UpdateSellerResult.NotAllowed, result);
        var unchangedSeller = await context.Sellers.SingleAsync();
        Assert.Equal("Ada", unchangedSeller.FirstName);
        Assert.Equal("deposit_validated", unchangedSeller.DepositStatus);
    }

    [Fact]
    public async Task CreateItemBatchAsync_RejectsValidatedSeller()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));
        await service.StartDepositAsync(seller.Id, await OpenDeskAsync(context));
        await service.ValidateSellerDepositAsync(seller.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateItemBatchAsync(new CreateItemBatchRequest(
                seller.Id,
                IntakeMode.EmailPreregistration,
                [new CreateItemRowRequest("Extra toy", 500)])));

        Assert.Equal("Seller deposit is already validated.", exception.Message);
        Assert.Single(await context.Items.ToListAsync());
    }

    [Fact]
    public async Task CancelSellerDepositValidationAsync_ReopensSellerAndMovesUnsoldStockItemsToRegistered()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest(
            "Ada",
            "Lovelace",
            null,
            "0123",
            "1 rue des Tests",
            null,
            PostalCode: "38000",
            City: "Grenoble",
            IdentityDocumentType: "CNI",
            IdentityDocumentNumber: "123456789",
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));
        await service.StartDepositAsync(seller.Id, await OpenDeskAsync(context));
        await service.ValidateSellerDepositAsync(seller.Id);

        var result = await service.CancelSellerDepositValidationAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.Changed, result);
        var reopenedSeller = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal("deposit_pending", reopenedSeller.DepositStatus);
        Assert.Equal("registered", Assert.Single(reopenedSeller.Items).Status);
    }

    [Fact]
    public async Task CancelSellerDepositValidationAsync_ReopensSellerAndLeavesHistoricalItemsReadOnly()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null, PostalCode: "38000", City: "Grenoble"));
        context.Items.AddRange(
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-001",
                Title = "Toy piano",
                SellerPriceCents = 1000,
                Status = "in_stock"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-002",
                Title = "Sold train",
                SellerPriceCents = 1200,
                Status = "sold"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-003",
                Title = "Missing puzzle",
                SellerPriceCents = 800,
                Status = "missing"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-004",
                Title = "Defective game",
                SellerPriceCents = 600,
                Status = "defective"
            });
        (await context.Sellers.SingleAsync()).DepositStatus = "deposit_validated";
        await context.SaveChangesAsync();

        var result = await service.CancelSellerDepositValidationAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.Changed, result);
        var reopenedSeller = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal("deposit_pending", reopenedSeller.DepositStatus);
        var statusesByReference = reopenedSeller.Items
            .OrderBy(x => x.Reference)
            // Every item seeded above has an explicit Reference, so the key is
            // never null here.
            .ToDictionary(x => x.Reference!, x => x.Status);
        Assert.Equal("registered", statusesByReference["AA-001"]);
        Assert.Equal("sold", statusesByReference["AA-002"]);
        Assert.Equal("missing", statusesByReference["AA-003"]);
        Assert.Equal("defective", statusesByReference["AA-004"]);
    }

    private static async Task<SellerEntity> CreateValidatableSellerAsync(AppDbContext context, IntakeService service)
    {
        var seller = await service.CreateSellerAsync(
            new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var tracked = await context.Sellers.SingleAsync(x => x.Id == seller.Id);
        tracked.PostalCode = "38120";
        tracked.City = "SAINT-EGREVE";
        tracked.IdentityDocumentType = "CNI";
        tracked.IdentityDocumentNumber = "123456";
        tracked.IdentityDocumentIssuedOn = new DateOnly(2020, 1, 1);
        await context.SaveChangesAsync();
        return tracked;
    }

    private static async Task<long> OpenDeskAsync(AppDbContext context)
    {
        return (await new DepositDeskService(context).OpenDeskAsync(null)).DeskId;
    }

    [Fact]
    public async Task StartDepositAsync_GivesExistingRowsAConsecutiveRunAndMarksTheDepositStarted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Puzzle", 500), new CreateItemRowRequest("Poupée", 300)]));
        var deskId = await OpenDeskAsync(context);

        var result = await service.StartDepositAsync(seller.Id, deskId);

        Assert.Equal(DepositAllocationStatus.Done, result.Status);
        Assert.Equal(["B001", "B002"], result.AssignedReferences);
        Assert.NotNull((await context.Sellers.SingleAsync(x => x.Id == seller.Id)).DepositStartedAt);
    }

    [Fact]
    public async Task StartDepositAsync_ForALiveSellerWithNoRowsOnlyMarksTheDepositStarted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);

        var result = await service.StartDepositAsync(seller.Id, await OpenDeskAsync(context));

        Assert.Equal(DepositAllocationStatus.Done, result.Status);
        Assert.Empty(result.AssignedReferences);
        Assert.NotNull((await context.Sellers.SingleAsync(x => x.Id == seller.Id)).DepositStartedAt);
    }

    [Fact]
    public async Task StartDepositAsync_RefusesASecondStartWithoutAllocating()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));
        var deskId = await OpenDeskAsync(context);
        await service.StartDepositAsync(seller.Id, deskId);

        var second = await service.StartDepositAsync(seller.Id, deskId);

        Assert.Equal(DepositAllocationStatus.NotAllowed, second.Status);
        Assert.Equal(1, (await context.ReferenceLetters.SingleAsync(x => x.Letter == "B")).LastNumber);
    }

    [Fact]
    public async Task StartDepositAsync_WithoutADeskWritesNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));

        var result = await service.StartDepositAsync(seller.Id, null);

        Assert.Equal(DepositAllocationStatus.NoDesk, result.Status);
        context.ChangeTracker.Clear();
        Assert.Null((await context.Sellers.SingleAsync(x => x.Id == seller.Id)).DepositStartedAt);
        Assert.Null((await context.Items.SingleAsync()).Reference);
    }

    [Fact]
    public async Task StartDepositAsync_WhenAnotherDeskTookTheSameReferencesFirstReturnsConflictAndWritesNothing()
    {
        // Two contexts on one database model two desks. The second one has
        // already read the letter counters, so it computes the same B001 the
        // first desk saves; the item.reference unique index rejects it.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var first = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await first.Database.EnsureCreatedAsync();
        await using var second = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        // Desks without letters, so both runs take the first free letter, B.
        var noLetters = Options.Create(new LabelsOptions { LettersPerDesk = 0 });
        var deskA = (await new DepositDeskService(first, noLetters).OpenDeskAsync("A")).DeskId;
        var deskB = (await new DepositDeskService(first, noLetters).OpenDeskAsync("B")).DeskId;
        var firstService = new IntakeService(first, labelsOptions: noLetters);
        var secondService = new IntakeService(second, labelsOptions: noLetters);
        var sellerA = await CreateValidatableSellerAsync(first, firstService);
        var sellerB = (await firstService.CreateSellerAsync(
            new CreateSellerRequest("Grace", "Hopper", null, "0456", "2 rue des Tests", null))).Seller!;
        await firstService.CreateItemBatchAsync(new CreateItemBatchRequest(sellerA.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));
        await firstService.CreateItemBatchAsync(new CreateItemBatchRequest(sellerB.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Train", 700)]));
        await second.ReferenceLetters.ToListAsync();

        Assert.Equal(DepositAllocationStatus.Done, (await firstService.StartDepositAsync(sellerA.Id, deskA)).Status);
        var result = await secondService.StartDepositAsync(sellerB.Id, deskB);

        Assert.Equal(DepositAllocationStatus.Conflict, result.Status);
        Assert.Empty(second.ChangeTracker.Entries());
        first.ChangeTracker.Clear();
        Assert.Null((await first.Sellers.SingleAsync(x => x.Id == sellerB.Id)).DepositStartedAt);
        Assert.Null((await first.Items.SingleAsync(x => x.SellerId == sellerB.Id)).Reference);
        var letterB = await first.ReferenceLetters.SingleAsync(x => x.Letter == "B");
        Assert.Equal(1, letterB.LastNumber);
        Assert.Equal(deskA, letterB.HeldByDeskId);
    }

    [Fact]
    public async Task StartDepositAsync_WhenAnotherDeskTookTheSameFreeLetterFirstReturnsConflictAndWritesNothing()
    {
        // Sellers with no rows: only the letter top-up collides, so this pins
        // the concurrency token path rather than the unique index.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var first = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await first.Database.EnsureCreatedAsync();
        await using var second = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var noLetters = Options.Create(new LabelsOptions { LettersPerDesk = 0 });
        var oneLetter = Options.Create(new LabelsOptions { LettersPerDesk = 1 });
        var deskA = (await new DepositDeskService(first, noLetters).OpenDeskAsync("A")).DeskId;
        var deskB = (await new DepositDeskService(first, noLetters).OpenDeskAsync("B")).DeskId;
        var firstService = new IntakeService(first, labelsOptions: oneLetter);
        var secondService = new IntakeService(second, labelsOptions: oneLetter);
        var sellerA = await CreateValidatableSellerAsync(first, firstService);
        var sellerB = (await firstService.CreateSellerAsync(
            new CreateSellerRequest("Grace", "Hopper", null, "0456", "2 rue des Tests", null))).Seller!;
        await second.ReferenceLetters.ToListAsync();

        Assert.Equal(DepositAllocationStatus.Done, (await firstService.StartDepositAsync(sellerA.Id, deskA)).Status);
        var result = await secondService.StartDepositAsync(sellerB.Id, deskB);

        Assert.Equal(DepositAllocationStatus.Conflict, result.Status);
        first.ChangeTracker.Clear();
        Assert.Null((await first.Sellers.SingleAsync(x => x.Id == sellerB.Id)).DepositStartedAt);
        Assert.Equal(deskA, (await first.ReferenceLetters.SingleAsync(x => x.Letter == "B")).HeldByDeskId);
        Assert.False(await first.ReferenceLetters.AnyAsync(x => x.HeldByDeskId == deskB));
    }

    [Fact]
    public async Task StartDepositAsync_WhenTheSameSellerWasStartedAtAnotherDeskFirstReturnsConflictAndKeepsTheFirstReferences()
    {
        // Two desks holding different letters start the same seller at once.
        // Neither the letter tokens nor the reference index fire, so only the
        // seller's start marker can stop the second save overwriting the
        // references the first desk already showed.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var first = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await first.Database.EnsureCreatedAsync();
        await using var second = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var oneLetter = Options.Create(new LabelsOptions { LettersPerDesk = 1 });
        var deskA = (await new DepositDeskService(first, oneLetter).OpenDeskAsync("A")).DeskId;
        var deskB = (await new DepositDeskService(first, oneLetter).OpenDeskAsync("B")).DeskId;
        var firstService = new IntakeService(first, labelsOptions: oneLetter);
        var secondService = new IntakeService(second, labelsOptions: oneLetter);
        var seller = await CreateValidatableSellerAsync(first, firstService);
        await firstService.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Puzzle", 500), new CreateItemRowRequest("Train", 700)]));
        // The second desk has already read the seller as not started.
        await second.Sellers.Include(x => x.Items).SingleAsync(x => x.Id == seller.Id);

        var firstResult = await firstService.StartDepositAsync(seller.Id, deskA);
        var result = await secondService.StartDepositAsync(seller.Id, deskB);

        Assert.Equal(DepositAllocationStatus.Done, firstResult.Status);
        Assert.Equal(DepositAllocationStatus.Conflict, result.Status);
        Assert.Empty(second.ChangeTracker.Entries());
        first.ChangeTracker.Clear();
        var references = await first.Items
            .Where(x => x.SellerId == seller.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Reference)
            .ToListAsync();
        Assert.Equal(firstResult.AssignedReferences, references);
        var deskBLetter = await first.ReferenceLetters.SingleAsync(x => x.HeldByDeskId == deskB);
        Assert.Equal(0, deskBLetter.LastNumber);
    }

    [Fact]
    public async Task StartDepositAsync_OnADeskClosedSinceItWasReadReturnsConflictAndLeavesNoLetterHeldByIt()
    {
        // The desk is read as open, then force-closed elsewhere, then the
        // allocation takes a free letter. The desk's closed_at token makes
        // that save fail instead of leaving a letter held by a closed desk.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var first = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await first.Database.EnsureCreatedAsync();
        await using var second = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var noLetters = Options.Create(new LabelsOptions { LettersPerDesk = 0 });
        var oneLetter = Options.Create(new LabelsOptions { LettersPerDesk = 1 });
        var deskId = (await new DepositDeskService(first, noLetters).OpenDeskAsync("A")).DeskId;
        var firstService = new IntakeService(first, labelsOptions: oneLetter);
        var secondService = new IntakeService(second, labelsOptions: oneLetter);
        var seller = await CreateValidatableSellerAsync(first, firstService);
        await second.DepositDesks.SingleAsync(x => x.Id == deskId);

        Assert.True(await new DepositDeskService(first, noLetters).CloseDeskAsync(deskId));
        var result = await secondService.StartDepositAsync(seller.Id, deskId);

        Assert.Equal(DepositAllocationStatus.Conflict, result.Status);
        first.ChangeTracker.Clear();
        Assert.False(await first.ReferenceLetters.AnyAsync(x => x.HeldByDeskId == deskId));
        Assert.Null((await first.Sellers.SingleAsync(x => x.Id == seller.Id)).DepositStartedAt);
    }

    [Fact]
    public async Task CreateItemBatchAsync_BeforeTheStartLeavesReferencesEmpty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);

        var result = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)], await OpenDeskAsync(context)));

        Assert.Equal(DepositAllocationStatus.Done, result.Status);
        Assert.Null(Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task CreateItemBatchAsync_AfterTheStartGivesEachRowTheNextNumberInTheSellerLetter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));
        var deskId = await OpenDeskAsync(context);
        await service.StartDepositAsync(seller.Id, deskId);

        var first = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Extra 1", 200)], deskId));
        var second = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Extra 2", 200)], deskId));

        Assert.Equal(["B002"], first.AssignedReferences);
        Assert.Equal(["B003"], second.AssignedReferences);
    }

    [Fact]
    public async Task CreateItemBatchAsync_OnAClosedDeskCreatesNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        var deskId = await OpenDeskAsync(context);
        await service.StartDepositAsync(seller.Id, deskId);
        await new DepositDeskService(context).CloseDeskAsync(deskId);

        var result = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Extra", 200)], deskId));

        Assert.Equal(DepositAllocationStatus.DeskClosed, result.Status);
        context.ChangeTracker.Clear();
        Assert.Equal(0, await context.Items.CountAsync());
    }

    [Fact]
    public async Task CancelSellerDepositValidationAsync_KeepsReferencesAndTheStartedMarker()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));
        await service.StartDepositAsync(seller.Id, await OpenDeskAsync(context));
        await service.ValidateSellerDepositAsync(seller.Id);

        await service.CancelSellerDepositValidationAsync(seller.Id);

        context.ChangeTracker.Clear();
        Assert.Equal("B001", (await context.Items.SingleAsync()).Reference);
        Assert.NotNull((await context.Sellers.SingleAsync()).DepositStartedAt);
    }

    [Fact]
    public async Task ValidateSellerDepositAsync_RefusesWhenARegisteredItemHasNoReference()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));

        // References were never allocated, simulating a direct POST to the
        // validate handler that bypasses the fee step's allocation call.
        var result = await service.ValidateSellerDepositAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.NotAllowed, result);
        var unchangedSeller = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal("deposit_pending", unchangedSeller.DepositStatus);
        Assert.Equal("registered", Assert.Single(unchangedSeller.Items).Status);
    }

    [Fact]
    public async Task ValidateSellerDepositAsync_IgnoresMissingReferencesOnNonRegisteredItemsFromAReopenedDeposit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        context.Items.AddRange(
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "B001",
                Title = "Puzzle",
                SellerPriceCents = 500,
                Status = "registered"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = null,
                Title = "Sold before this scheme existed",
                SellerPriceCents = 300,
                Status = "sold"
            });
        await context.SaveChangesAsync();

        var result = await service.ValidateSellerDepositAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.Changed, result);
    }

    private static IserePostalCityLookup CreateLookup(string postalCode, params string[] cities)
    {
        return new IserePostalCityLookup(
            new Dictionary<string, IReadOnlyList<string>>
            {
                [postalCode] = cities
            });
    }
}


