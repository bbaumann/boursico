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
        Assert.Null(seller.IdentityDocumentIssuer);
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
            seller.Items.OrderBy(x => x.ItemNumber),
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
            createdItems.OrderBy(item => item.ItemNumber),
            first =>
            {
                Assert.Equal(1, first.ItemNumber);
                Assert.Null(first.Reference);
                Assert.Equal(500, first.SellerPriceCents);
                Assert.Equal("registered", first.Status);
            },
            second =>
            {
                Assert.Equal(2, second.ItemNumber);
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

        Assert.All(created, item => Assert.Null(item.Reference));
        Assert.All(created, item => Assert.Equal("registered", item.Status));
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
            [new CreateItemRowRequest("Toy piano", 1000)]))).Single();

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
            ItemNumber = 1,
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
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15),
            IdentityDocumentIssuer: "Préfecture 38"));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [
                new CreateItemRowRequest("Toy piano", 1000),
                new CreateItemRowRequest("Wooden train", 1200)
            ]));

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
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15),
            IdentityDocumentIssuer: "Préfecture 38"));
        context.Items.Add(new ItemEntity
        {
            SellerId = seller.Id,
            Reference = "BB001",
            ItemNumber = 1,
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
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15),
            IdentityDocumentIssuer: "Préfecture 38"));
        context.Items.AddRange(
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-001",
                ItemNumber = 1,
                Title = "Toy piano",
                SellerPriceCents = 1000,
                Status = "registered"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-002",
                ItemNumber = 2,
                Title = "Sold train",
                SellerPriceCents = 1200,
                Status = "sold"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-003",
                ItemNumber = 3,
                Title = "Missing puzzle",
                SellerPriceCents = 800,
                Status = "missing"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-004",
                ItemNumber = 4,
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
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15),
            IdentityDocumentIssuer: "Préfecture 38"));

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
    [InlineData("identity_document_issuer")]
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
            IdentityDocumentIssuedOn: missingField == "identity_document_issued_on" ? null : new DateOnly(2020, 1, 15),
            IdentityDocumentIssuer: missingField == "identity_document_issuer" ? null : "Préfecture 38"));
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
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15),
            IdentityDocumentIssuer: "Préfecture 38"));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));
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
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15),
            IdentityDocumentIssuer: "Préfecture 38"));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));
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
            IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15),
            IdentityDocumentIssuer: "Préfecture 38"));
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Toy piano", 1000)]));
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
                ItemNumber = 1,
                Title = "Toy piano",
                SellerPriceCents = 1000,
                Status = "in_stock"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-002",
                ItemNumber = 2,
                Title = "Sold train",
                SellerPriceCents = 1200,
                Status = "sold"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-003",
                ItemNumber = 3,
                Title = "Missing puzzle",
                SellerPriceCents = 800,
                Status = "missing"
            },
            new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "AA-004",
                ItemNumber = 4,
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

    private static IserePostalCityLookup CreateLookup(string postalCode, params string[] cities)
    {
        return new IserePostalCityLookup(
            new Dictionary<string, IReadOnlyList<string>>
            {
                [postalCode] = cities
            });
    }
}


