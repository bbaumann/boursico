using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Http;

namespace AssociationSaleTool.Web.Tests.Resources;

public sealed class DepotWorkbookPostalCityTests
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace OfficeRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    [Fact]
    public async Task DepotWorkbook_UsesCanonicalPostalCityValuesFromApplicationJson()
    {
        var workbookPath = FindRepositoryFile("resources", "depot-internet.xlsx");
        var jsonPath = FindRepositoryFile("src", "AssociationSaleTool.Web", "wwwroot", "data", "isere-postal-cities.json");
        var expectedValues = await ReadExpectedPostalCityValuesAsync(jsonPath);

        using var archive = ZipFile.OpenRead(workbookPath);
        var sheetPath = GetWorksheetPathByName(archive, "codes postaux");
        var worksheet = LoadXml(archive, sheetPath);
        var sharedStrings = LoadSharedStrings(archive);

        var actualValues = worksheet
            .Descendants(SpreadsheetNamespace + "c")
            .Where(cell => GetColumnName((string?)cell.Attribute("r")) == "D")
            .Select(cell => ReadCellText(cell, sharedStrings))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();

        Assert.Equal(expectedValues, actualValues);
    }

    [Fact]
    public async Task DepotWorkbook_ParsesWithApplicationPostalCityLookup()
    {
        var workbookPath = FindRepositoryFile("resources", "depot-internet.xlsx");
        var jsonPath = FindRepositoryFile("src", "AssociationSaleTool.Web", "wwwroot", "data", "isere-postal-cities.json");
        var expectedValues = await ReadExpectedPostalCityValuesAsync(jsonPath);
        var lookup = IserePostalCityLookup.LoadFromJsonFile(jsonPath);
        var parser = new ExcelSellerItemParser(lookup);

        await using var stream = File.OpenRead(workbookPath);
        var file = new FormFile(stream, 0, stream.Length, "ExcelFile", "depot-internet.xlsx");

        var result = await parser.ParseAsync(file);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Contains($"{result.PostalCode} {result.City}", expectedValues);
    }

    private static async Task<List<string>> ReadExpectedPostalCityValuesAsync(string jsonPath)
    {
        await using var stream = File.OpenRead(jsonPath);
        var citiesByPostalCode = await JsonSerializer.DeserializeAsync<Dictionary<string, string[]>>(stream)
            ?? new Dictionary<string, string[]>(StringComparer.Ordinal);

        return citiesByPostalCode
            .SelectMany(pair => pair.Value.Select(city => $"{pair.Key} {city}"))
            .ToList();
    }

    private static string GetWorksheetPathByName(ZipArchive archive, string sheetName)
    {
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var sheet = workbook
            .Descendants(SpreadsheetNamespace + "sheet")
            .FirstOrDefault(candidate => string.Equals((string?)candidate.Attribute("name"), sheetName, StringComparison.Ordinal));

        if (sheet is null)
        {
            throw new InvalidOperationException($"Workbook sheet '{sheetName}' was not found.");
        }

        var relationshipId = (string?)sheet.Attribute(OfficeRelationshipsNamespace + "id");
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            throw new InvalidOperationException($"Workbook sheet '{sheetName}' has no relationship id.");
        }

        var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        var target = relationships
            .Descendants(RelationshipsNamespace + "Relationship")
            .Where(relationship => string.Equals((string?)relationship.Attribute("Id"), relationshipId, StringComparison.Ordinal))
            .Select(relationship => (string?)relationship.Attribute("Target"))
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException($"Workbook sheet '{sheetName}' has no worksheet target.");
        }

        if (target.StartsWith('/'))
        {
            return target.TrimStart('/');
        }

        return target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? target
            : $"xl/{target}";
    }

    private static XDocument LoadXml(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"Workbook entry '{entryName}' was not found.");

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static List<string> LoadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var sharedStrings = XDocument.Load(stream);
        return sharedStrings
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToList();
    }

    private static string? ReadCellText(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value));
        }

        var value = cell.Element(SpreadsheetNamespace + "v")?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value, out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        return value;
    }

    private static string? GetColumnName(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return null;
        }

        return new string(cellReference.TakeWhile(char.IsLetter).ToArray());
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
