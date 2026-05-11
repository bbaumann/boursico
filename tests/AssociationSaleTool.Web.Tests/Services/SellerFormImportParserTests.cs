using System.Text;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Http;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class SellerFormImportParserTests
{
    [Fact]
    public async Task ParseAsync_ReadsFramaformsExportAndNormalizesRows()
    {
        var parser = new SellerFormImportParser(CreateLookup("38120", "Saint-Égrève"));

        var result = await parser.ParseAsync(CreateCsv(SampleCsv));

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Collection(
            result.Submissions,
            first =>
            {
                Assert.Equal("Benjamin", first.FirstName);
                Assert.Equal("Baumann", first.LastName);
                Assert.Equal("0609710985", first.Phone);
                Assert.Equal("Zentiltoutou@Gmail.com", first.Email);
                Assert.Equal("29 avenue du mont Valérien", first.PostalAddress);
                Assert.Equal("92500", first.PostalCode);
                Assert.Equal(string.Empty, first.City);
                Assert.Contains("Rueil Malmaison", first.ImportNotes);
                var item = Assert.Single(first.ValidRows);
                Assert.Equal("Lego Pirate", item.Title);
                Assert.Equal(500, item.SellerPriceCents);
                Assert.Contains(first.IgnoredItemLines, line => line.RawValue == "Puzzle incomplet");
                Assert.Contains(first.IgnoredItemLines, line => line.RawValue == "Voiture 4,50");
            },
            second =>
            {
                Assert.Equal("Sarah", second.FirstName);
                Assert.Equal("Bulteau", second.LastName);
                Assert.Equal("38120", second.PostalCode);
                Assert.Equal("Saint-Égrève", second.City);
                Assert.Null(second.ImportNotes);
                Assert.Collection(
                    second.ValidRows,
                    firstItem =>
                    {
                        Assert.Equal("Lot 3 livres Monsieur Madame", firstItem.Title);
                        Assert.Equal(200, firstItem.SellerPriceCents);
                    },
                    secondItem =>
                    {
                        Assert.Equal("Chateau Playmobil", secondItem.Title);
                        Assert.Equal(1000, secondItem.SellerPriceCents);
                    });
            });
    }

    [Fact]
    public async Task ParseAsync_ImportsSellerDraftWhenNoItemLineIsValid()
    {
        var parser = new SellerFormImportParser();

        var result = await parser.ParseAsync(CreateCsv(
            """
            "Export test","","","","","","","","","","","","","","","",""
            "Détails de la soumission","","","","","","","","","","","","","","","",""
            "Séquentiel","SID","Heure de soumission","Heure de complétion","Heure de modification","Brouillon","Adresse IP","UID","Nom d'utilisateur","Nom","Prénom","Téléphone","Email","Adresse","Code Postal","Ville","Jouet,Prix"
            "1","1","","","","0","","","","Durand","Marie","","","","","","Puzzle sans prix"
            """));

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        var submission = Assert.Single(result.Submissions);
        Assert.Empty(submission.ValidRows);
        Assert.Contains("aucun article valide", submission.ImportNotes);
    }

    private const string SampleCsv =
        """
        "Bourse aux jouets Test","","","","","","","","","","","","","","","",""
        "Détails de la soumission","","","","","","","","","","","","","","","",""
        "Séquentiel","SID","Heure de soumission","Heure de complétion","Heure de modification","Brouillon","Adresse IP","UID","Nom d'utilisateur","Nom","Prénom","Téléphone","Email","Adresse","Code Postal","Ville","Jouet,Prix"
        "1","33792452","11/05/2026 - 18:20","11/05/2026 - 18:20","11/05/2026 - 19:33","0","91.167.159.160","2780872","Zentiltoutou","Baumann","Benjamin","0609710985","Zentiltoutou@Gmail.com","29 avenue du mont Valérien","92500","Rueil Malmaison","Lego Pirate, 5
        Puzzle incomplet
        Voiture 4,50"
        "2","33792475","11/05/2026 - 18:20","11/05/2026 - 18:20","11/05/2026 - 19:33","0","91.167.159.160","2780872","Zentiltoutou","Bulteau","Sarah","0600000000","sarah.bulteau@example.test","12 Rue Adolphe Muguet","38120","St Egreve","Lot 3 livres Monsieur Madame, 2
        Chateau Playmobil, 10"
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
