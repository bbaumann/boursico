using AssociationSaleTool.Web.Services;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class IserePostalCityLookupTests
{
    [Fact]
    public void TryFindCanonicalCity_AcceptsExactCityForPostalCode()
    {
        var lookup = new IserePostalCityLookup(
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["38120"] = ["Saint-Égrève"]
            });

        var matched = lookup.TryFindCanonicalCity("38120", "Saint-Égrève", out var city);

        Assert.True(matched);
        Assert.Equal("Saint-Égrève", city);
    }

    [Fact]
    public void TryFindCanonicalCity_RejectsAbbreviatedOrHyphenlessCity()
    {
        var lookup = new IserePostalCityLookup(
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["38120"] = ["Saint-Égrève"]
            });

        var matched = lookup.TryFindCanonicalCity("38120", "ST EGREVE", out var city);

        Assert.False(matched);
        Assert.Null(city);
    }

    [Fact]
    public void LoadFromJsonFile_ReadsAuthoritativeIsereData()
    {
        var jsonPath = FindRepositoryFile(
            "src",
            "AssociationSaleTool.Web",
            "wwwroot",
            "data",
            "isere-postal-cities.json");

        var lookup = IserePostalCityLookup.LoadFromJsonFile(jsonPath);

        Assert.True(lookup.TryFindCanonicalCity("38300", "Bourgoin-Jallieu", out var city));
        Assert.Equal("Bourgoin-Jallieu", city);
    }

    private static string FindRepositoryFile(params string[] relativePathParts)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativePathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file {Path.Combine(relativePathParts)}.");
    }
}
