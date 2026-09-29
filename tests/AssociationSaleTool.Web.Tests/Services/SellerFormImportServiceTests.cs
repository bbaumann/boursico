using System.Text;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class SellerFormImportServiceTests
{
    [Fact]
    public async Task ImportAsync_CreatesPermissiveSellerDraftsAndImportNotes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new SellerFormImportService(
            context,
            new SellerFormImportParser(CreateLookup("38120", "Saint-Égrève")));

        var result = await service.ImportAsync(CreateCsv(SampleCsv));

        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.Equal(SellerFormImportRowOutcome.Imported, row.Outcome));
        var sellers = await context.Sellers.Include(x => x.Items).OrderBy(x => x.SellerCode).ToListAsync();
        Assert.Collection(
            sellers,
            first =>
            {
                Assert.Equal("Benjamin", first.FirstName);
                Assert.Equal("Baumann", first.LastName);
                Assert.Equal("92500", first.PostalCode);
                Assert.Equal(string.Empty, first.City);
                Assert.Contains("Rueil Malmaison", first.ImportNotes);
                Assert.Equal("deposit_pending", first.DepositStatus);
                Assert.Equal(0, first.FeePaidCents);
                Assert.Single(first.Items);
            },
            second =>
            {
                Assert.Equal("Sarah", second.FirstName);
                Assert.Equal("Bulteau", second.LastName);
                Assert.Equal("38120", second.PostalCode);
                Assert.Equal("Saint-Égrève", second.City);
                Assert.Null(second.ImportNotes);
                Assert.Equal(2, second.Items.Count);
            });
    }

    [Fact]
    public async Task ImportAsync_AllocatesTwoLetterCodesIgnoringMalformedAndReservedCodes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Sellers.AddRange(
            new SellerEntity
            {
                SellerCode = "DL4BCF74",
                FirstName = "Legacy",
                LastName = "LongCode",
                Phone = "0101010101",
                PostalAddress = "1 rue des Tests"
            },
            new SellerEntity
            {
                SellerCode = "CK-001",
                FirstName = "Legacy",
                LastName = "HyphenCode",
                Phone = "0202020202",
                PostalAddress = "2 rue des Tests"
            },
            new SellerEntity
            {
                SellerCode = "AF",
                FirstName = "Association",
                LastName = "Familiale",
                Phone = "0303030303",
                PostalAddress = "3 rue des Tests"
            });
        await context.SaveChangesAsync();
        var service = new SellerFormImportService(
            context,
            new SellerFormImportParser(CreateLookup("38120", "Saint-Ã‰grÃ¨ve")));

        var result = await service.ImportAsync(CreateCsv(SampleCsv));

        Assert.All(result.Rows, row => Assert.Equal(SellerFormImportRowOutcome.Imported, row.Outcome));
        var importedSellers = await context.Sellers
            .Where(x => x.LastName == "Baumann" || x.LastName == "Bulteau")
            .OrderBy(x => x.SellerCode)
            .ToListAsync();
        Assert.Collection(
            importedSellers,
            first =>
            {
                Assert.Equal("BB", first.SellerCode);
                Assert.Equal("Baumann", first.LastName);
            },
            second =>
            {
                Assert.Equal("CB", second.SellerCode);
                Assert.Equal("Bulteau", second.LastName);
            });
    }

    [Fact]
    public async Task ImportAsync_CreatesIncompleteItemsWhenItemTitleCanBeRecovered()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var service = new SellerFormImportService(context, new SellerFormImportParser());

        var result = await service.ImportAsync(CreateCsv(RecoverableItemErrorsCsv));

        var row = Assert.Single(result.Rows);
        Assert.Equal(SellerFormImportRowOutcome.Imported, row.Outcome);
        var seller = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal("Camille", seller.FirstName);
        Assert.Collection(
            seller.Items.OrderBy(x => x.Id),
            first =>
            {
                Assert.Equal("Puzzle complet", first.Title);
                Assert.Equal(500, first.SellerPriceCents);
            },
            second =>
            {
                Assert.Equal("Jeu prix decimal", second.Title);
                int? price = second.SellerPriceCents;
                Assert.Null(price);
            },
            third =>
            {
                Assert.Equal("Jouet prix absent", third.Title);
                int? price = third.SellerPriceCents;
                Assert.Null(price);
            });
        Assert.Contains("prix non accepté", seller.ImportNotes);
        Assert.Contains("prix manquant", seller.ImportNotes);
        Assert.Contains("description manquante", seller.ImportNotes);
    }

    [Fact]
    public async Task ImportAsync_FlagsSingleDuplicateWithoutAutoImportingIt()
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
            SellerCode = "AA",
            FirstName = "Sarah",
            LastName = "Bulteau",
            Phone = "0600000000",
            PostalAddress = "12 Rue Adolphe Muguet",
            PostalCode = "38120",
            City = "Saint-Égrève"
        });
        await context.SaveChangesAsync();
        var service = new SellerFormImportService(
            context,
            new SellerFormImportParser(CreateLookup("38120", "Saint-Égrève")));

        var result = await service.ImportAsync(CreateCsv(SingleDuplicateCsv));

        var row = Assert.Single(result.Rows);
        Assert.Equal(SellerFormImportRowOutcome.Duplicate, row.Outcome);
        Assert.True(row.CanAppendToExistingSeller);
        Assert.Equal("AA", Assert.Single(row.DuplicateMatches).SellerCode);
        Assert.Equal(1, await context.Sellers.CountAsync());
        Assert.Empty(await context.Items.ToListAsync());
    }

    [Fact]
    public async Task AppendDuplicateItemsAsync_IgnoresExistingExactTitleAndPrice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var seller = new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Sarah",
            LastName = "Bulteau",
            Phone = "0600000000",
            PostalAddress = "12 Rue Adolphe Muguet",
            PostalCode = "38120",
            City = "Saint-Égrève"
        };
        seller.Items.Add(new ItemEntity
        {
            Reference = "AA001",
            Title = "Lego Pirate",
            SellerPriceCents = 500,
            Status = "registered"
        });
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();
        var service = new SellerFormImportService(context, new SellerFormImportParser());

        var result = await service.AppendDuplicateItemsAsync(
            seller.Id,
            [
                new CreateItemRowRequest("Lego Pirate", 500),
                new CreateItemRowRequest("Jeu PS5", 500)
            ],
            "Import formulaire : ligne ignorée.");

        Assert.Equal(SellerFormDuplicateAppendOutcome.Appended, result.Outcome);
        Assert.Equal(1, result.CreatedItemCount);
        Assert.Equal(1, result.IgnoredExistingItemCount);
        var updated = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Equal(2, updated.Items.Count);
        Assert.Contains(updated.Items, item => item.Reference is null && item.Title == "Jeu PS5");
        Assert.Contains("ligne ignorée", updated.ImportNotes);
        Assert.Contains("article déjà présent ignoré - Lego Pirate - 5 €", updated.ImportNotes);
    }

    [Fact]
    public async Task AppendDuplicateItemsAsync_AddsIncompleteRowsWithMissingPrices()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var seller = new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Sarah",
            LastName = "Bulteau",
            Phone = "0600000000",
            PostalAddress = "12 Rue Adolphe Muguet",
            PostalCode = "38120",
            City = "Saint-Ã‰grÃ¨ve"
        };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();
        var service = new SellerFormImportService(context, new SellerFormImportParser());

        var result = await service.AppendDuplicateItemsAsync(
            seller.Id,
            [
                new CreateItemRowRequest("Jeu sans prix", null),
                new CreateItemRowRequest("Jeu PS5", 500)
            ],
            "Import formulaire : prix Ã  corriger.");

        Assert.Equal(SellerFormDuplicateAppendOutcome.Appended, result.Outcome);
        Assert.Equal(2, result.CreatedItemCount);
        var updated = await context.Sellers.Include(x => x.Items).SingleAsync();
        Assert.Collection(
            updated.Items.OrderBy(x => x.Id),
            first =>
            {
                Assert.Equal("Jeu sans prix", first.Title);
                int? price = first.SellerPriceCents;
                Assert.Null(price);
            },
            second =>
            {
                Assert.Equal("Jeu PS5", second.Title);
                Assert.Equal(500, second.SellerPriceCents);
            });
        Assert.Contains("prix Ã  corriger", updated.ImportNotes);
    }

    [Fact]
    public async Task ImportAsync_ExposesOnlyIgnoreForSeveralDuplicateMatches()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Sellers.AddRange(
            new SellerEntity { SellerCode = "AA", FirstName = "Sarah", LastName = "Bulteau", Phone = "0600000000" },
            new SellerEntity { SellerCode = "BB", FirstName = "Sarah", LastName = "Bulteau", Phone = "0600000000" });
        await context.SaveChangesAsync();
        var service = new SellerFormImportService(
            context,
            new SellerFormImportParser(CreateLookup("38120", "Saint-Égrève")));

        var result = await service.ImportAsync(CreateCsv(SingleDuplicateCsv));

        var row = Assert.Single(result.Rows);
        Assert.Equal(SellerFormImportRowOutcome.Duplicate, row.Outcome);
        Assert.False(row.CanAppendToExistingSeller);
        Assert.Equal(2, row.DuplicateMatches.Count);
        Assert.Equal(2, await context.Sellers.CountAsync());
    }

    private const string SampleCsv =
        """
        "Bourse aux jouets Test","","","","","","","","","","","","","","","",""
        "Détails de la soumission","","","","","","","","","","","","","","","",""
        "Séquentiel","SID","Heure de soumission","Heure de complétion","Heure de modification","Brouillon","Adresse IP","UID","Nom d'utilisateur","Nom","Prénom","Téléphone","Email","Adresse","Code Postal","Ville","Jouet,Prix"
        "1","33792452","11/05/2026 - 18:20","11/05/2026 - 18:20","11/05/2026 - 19:33","0","91.167.159.160","2780872","Zentiltoutou","Baumann","Benjamin","0609710985","Zentiltoutou@Gmail.com","29 avenue du mont Valérien","92500","Rueil Malmaison","Lego Pirate, 5"
        "2","33792475","11/05/2026 - 18:20","11/05/2026 - 18:20","11/05/2026 - 19:33","0","91.167.159.160","2780872","Zentiltoutou","Bulteau","Sarah","0600000000","sarah.bulteau@example.test","12 Rue Adolphe Muguet","38120","St Egreve","Lot 3 livres Monsieur Madame, 2
        Chateau Playmobil, 10"
        """;

    private const string RecoverableItemErrorsCsv =
        """
        "Bourse aux jouets Test","","","","","","","","","","","","","","","",""
        "Détails de la soumission","","","","","","","","","","","","","","","",""
        "Séquentiel","SID","Heure de soumission","Heure de complétion","Heure de modification","Brouillon","Adresse IP","UID","Nom d'utilisateur","Nom","Prénom","Téléphone","Email","Adresse","Code Postal","Ville","Jouet,Prix"
        "1","33793000","11/05/2026 - 18:20","11/05/2026 - 18:20","11/05/2026 - 19:33","0","91.167.159.160","2780872","Test","Martin","Camille","0600000001","camille@example.test","1 rue des tests","38100","Grenoble","Puzzle complet, 5
        Jeu prix decimal, 4,50
        Jouet prix absent
        12"
        """;

    private const string SingleDuplicateCsv =
        """
        "Bourse aux jouets Test","","","","","","","","","","","","","","","",""
        "Détails de la soumission","","","","","","","","","","","","","","","",""
        "Séquentiel","SID","Heure de soumission","Heure de complétion","Heure de modification","Brouillon","Adresse IP","UID","Nom d'utilisateur","Nom","Prénom","Téléphone","Email","Adresse","Code Postal","Ville","Jouet,Prix"
        "1","33792475","11/05/2026 - 18:20","11/05/2026 - 18:20","11/05/2026 - 19:33","0","91.167.159.160","2780872","Zentiltoutou","Bulteau","Sarah","0600000000","sarah.bulteau@example.test","12 Rue Adolphe Muguet","38120","St Egreve","Lego Pirate, 5
        Jeu PS5, 5"
        """;

    private static FormFile CreateCsv(string contents)
    {
        var bytes = Encoding.UTF8.GetBytes(contents);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "FormFile", "export.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }

    private static IserePostalCityLookup CreateLookup(string postalCode, string city)
    {
        return new IserePostalCityLookup(
            new Dictionary<string, IReadOnlyList<string>>
            {
                [postalCode] = [city]
            });
    }
}
