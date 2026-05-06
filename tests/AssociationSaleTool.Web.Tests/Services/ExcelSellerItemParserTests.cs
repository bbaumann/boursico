using System.IO.Compression;
using System.Security;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Http;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class ExcelSellerItemParserTests
{
    [Fact]
    public async Task ParseAsync_MapsOneSheetTemplateWithRowsBeyondFifteen()
    {
        var file = CreateWorkbook(
            strings:
            [
                ("C2", "NOM PRENOM"),
                ("C3", "DURAND Marie"),
                ("H2", "TELEPHONE"),
                ("H3", "0600000000"),
                ("B5", "libellé"),
                ("C5", "prix")
            ],
            numbers: Enumerable.Range(7, 17)
                .Select(row => ($"C{row}", (decimal)(row - 6)))
                .ToArray(),
            extraStrings: Enumerable.Range(7, 17)
                .Select(row => ($"B{row}", $"Jouet {row}"))
                .ToArray());
        var parser = new ExcelSellerItemParser();

        var result = await parser.ParseAsync(file);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal("Marie", result.FirstName);
        Assert.Equal("Durand", result.LastName);
        Assert.Equal("0600000000", result.Phone);
        Assert.Equal("UNKNOWN", result.PostalAddress);
        Assert.Equal(17, result.Rows.Count);
        Assert.Equal("Jouet 7", result.Rows[0].Title);
        Assert.Equal(100, result.Rows[0].SellerPriceCents);
        Assert.Equal("Jouet 23", result.Rows[^1].Title);
        Assert.Equal(1700, result.Rows[^1].SellerPriceCents);
    }

    [Fact]
    public async Task ParseAsync_MapsCanonicalPostalCodeAndCity()
    {
        var file = CreateWorkbook(
            strings:
            [
                ("C2", "NOM PRENOM"),
                ("C3", "DURAND Marie"),
                ("F2", "CP VILLE"),
                ("F3", "38120 Saint-Égrève"),
                ("B5", "libellé"),
                ("C5", "prix"),
                ("B7", "Puzzle")
            ],
            numbers: [("C7", 5m)]);
        var parser = new ExcelSellerItemParser(CreateLookup("38120", "Saint-Égrève"));

        var result = await parser.ParseAsync(file);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal("38120", result.PostalCode);
        Assert.Equal("Saint-Égrève", result.City);
    }

    [Fact]
    public async Task ParseAsync_RejectsPostalCityOutsideAuthoritativeList()
    {
        var file = CreateWorkbook(
            strings:
            [
                ("C2", "NOM PRENOM"),
                ("C3", "DURAND Marie"),
                ("F2", "CP VILLE"),
                ("F3", "38120 ST EGREVE"),
                ("B5", "libellé"),
                ("C5", "prix"),
                ("B7", "Puzzle")
            ],
            numbers: [("C7", 5m)]);
        var parser = new ExcelSellerItemParser(CreateLookup("38120", "Saint-Égrève"));

        var result = await parser.ParseAsync(file);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "La ville \"ST EGREVE\" n'est pas connue pour le code postal 38120 dans la liste officielle de l'Isère.",
            result.Errors);
    }

    [Fact]
    public async Task ParseAsync_RestoresLeadingZeroWhenPhoneIsStoredAsNumber()
    {
        var file = CreateWorkbook(
            strings:
            [
                ("C2", "NOM PRENOM"),
                ("C3", "DURAND Marie"),
                ("H2", "TELEPHONE"),
                ("B5", "libellé"),
                ("C5", "prix"),
                ("B7", "Puzzle")
            ],
            numbers: [("H3", 600000000m), ("C7", 5m)]);
        var parser = new ExcelSellerItemParser();

        var result = await parser.ParseAsync(file);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal("0600000000", result.Phone);
    }

    [Fact]
    public async Task ParseAsync_StopsAtFirstBlankProductRow()
    {
        var file = CreateWorkbook(
            strings:
            [
                ("C2", "NOM PRENOM"),
                ("C3", "DURAND Marie"),
                ("B5", "libellé"),
                ("C5", "prix"),
                ("B7", "Puzzle"),
                ("B9", "Footer text")
            ],
            numbers: [("C7", 5m), ("C9", 9m)]);
        var parser = new ExcelSellerItemParser();

        var result = await parser.ParseAsync(file);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors));
        var row = Assert.Single(result.Rows);
        Assert.Equal("Puzzle", row.Title);
        Assert.Equal(500, row.SellerPriceCents);
    }

    [Fact]
    public async Task ParseAsync_RejectsMissingSellerName()
    {
        var file = CreateWorkbook(
            strings:
            [
                ("C2", "NOM PRENOM"),
                ("C3", "A SAISIR"),
                ("B5", "libellé"),
                ("C5", "prix"),
                ("B7", "Puzzle")
            ],
            numbers: [("C7", 5m)]);
        var parser = new ExcelSellerItemParser();

        var result = await parser.ParseAsync(file);

        Assert.False(result.Succeeded);
        Assert.Contains("Le nom du vendeur est introuvable dans le fichier Excel.", result.Errors);
    }

    [Fact]
    public async Task ParseAsync_RejectsTitleWithoutPrice()
    {
        var file = CreateWorkbook(
            strings:
            [
                ("C2", "NOM PRENOM"),
                ("C3", "DURAND Marie"),
                ("B5", "libellé"),
                ("C5", "prix"),
                ("B7", "Puzzle")
            ],
            numbers: []);
        var parser = new ExcelSellerItemParser();

        var result = await parser.ParseAsync(file);

        Assert.False(result.Succeeded);
        Assert.Contains("La ligne 7 contient un titre sans prix vendeur.", result.Errors);
    }

    [Fact]
    public async Task ParseAsync_RejectsPriceWithoutTitle()
    {
        var file = CreateWorkbook(
            strings:
            [
                ("C2", "NOM PRENOM"),
                ("C3", "DURAND Marie"),
                ("B5", "libellé"),
                ("C5", "prix")
            ],
            numbers: [("C7", 5m)]);
        var parser = new ExcelSellerItemParser();

        var result = await parser.ParseAsync(file);

        Assert.False(result.Succeeded);
        Assert.Contains("La ligne 7 contient un prix vendeur sans titre.", result.Errors);
    }

    [Fact]
    public async Task ParseAsync_RejectsNonXlsxFiles()
    {
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "liste.xlsm");
        var parser = new ExcelSellerItemParser();

        var result = await parser.ParseAsync(file);

        Assert.False(result.Succeeded);
        Assert.Contains("Le fichier doit être au format .xlsx.", result.Errors);
    }

    [Fact]
    public async Task ParseAsync_RejectsWorkbookWithMultipleSheets()
    {
        var file = CreateWorkbook(
            strings:
            [
                ("C2", "NOM PRENOM"),
                ("C3", "DURAND Marie"),
                ("B5", "libellé"),
                ("C5", "prix"),
                ("B7", "Puzzle")
            ],
            numbers: [("C7", 5m)],
            sheetCount: 2);
        var parser = new ExcelSellerItemParser();

        var result = await parser.ParseAsync(file);

        Assert.False(result.Succeeded);
        Assert.Contains("Le fichier Excel doit contenir une seule feuille vendeur.", result.Errors);
    }

    [Fact]
    public async Task ParseAsync_IgnoresHiddenSheetsWhenCountingSellerWorksheets()
    {
        var file = CreateWorkbook(
            strings:
            [
                ("C2", "NOM PRENOM"),
                ("C3", "DURAND Marie"),
                ("B5", "libelle"),
                ("C5", "prix"),
                ("B7", "Puzzle")
            ],
            numbers: [("C7", 5m)],
            sheetCount: 2,
            hiddenSheetIndexes: new HashSet<int> { 2 });
        var parser = new ExcelSellerItemParser();

        var result = await parser.ParseAsync(file);

        Assert.True(result.Succeeded);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Puzzle", row.Title);
        Assert.Equal(500, row.SellerPriceCents);
    }

    private static IserePostalCityLookup CreateLookup(string postalCode, params string[] cities)
    {
        return new IserePostalCityLookup(
            new Dictionary<string, IReadOnlyList<string>>
            {
                [postalCode] = cities
            });
    }

    private static IFormFile CreateWorkbook(
        IReadOnlyList<(string Cell, string Value)> strings,
        IReadOnlyList<(string Cell, decimal Value)> numbers,
        IReadOnlyList<(string Cell, string Value)>? extraStrings = null,
        int sheetCount = 1,
        IReadOnlySet<int>? hiddenSheetIndexes = null)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml", ContentTypesXml(sheetCount));
            AddEntry(archive, "_rels/.rels", RootRelsXml());
            AddEntry(archive, "xl/workbook.xml", WorkbookXml(sheetCount, hiddenSheetIndexes));
            AddEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml(sheetCount));
            AddEntry(archive, "xl/worksheets/sheet1.xml", SheetXml(strings.Concat(extraStrings ?? []).ToArray(), numbers));

            for (var sheetIndex = 2; sheetIndex <= sheetCount; sheetIndex++)
            {
                AddEntry(archive, $"xl/worksheets/sheet{sheetIndex}.xml", SheetXml([], []));
            }
        }

        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "ExcelFile", "liste.xlsx");
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static string ContentTypesXml(int sheetCount)
    {
        var sheets = string.Join(
            "",
            Enumerable.Range(1, sheetCount)
                .Select(index => $"<Override PartName=\"/xl/worksheets/sheet{index}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              {sheets}
            </Types>
            """;
    }

    private static string RootRelsXml()
    {
        return """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """;
    }

    private static string WorkbookXml(int sheetCount, IReadOnlySet<int>? hiddenSheetIndexes)
    {
        var sheets = string.Join(
            "",
            Enumerable.Range(1, sheetCount)
                .Select(index =>
                {
                    var state = hiddenSheetIndexes?.Contains(index) == true ? " state=\"hidden\"" : string.Empty;
                    var name = index == 2 && state.Length > 0 ? "codes postaux" : $"Feuil{index}";
                    return $"<sheet name=\"{name}\" sheetId=\"{index}\"{state} r:id=\"rId{index}\"/>";
                }));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>{sheets}</sheets>
            </workbook>
            """;
    }

    private static string WorkbookRelsXml(int sheetCount)
    {
        var rels = string.Join(
            "",
            Enumerable.Range(1, sheetCount)
                .Select(index => $"<Relationship Id=\"rId{index}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{index}.xml\"/>"));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{rels}</Relationships>
            """;
    }

    private static string SheetXml(
        IReadOnlyList<(string Cell, string Value)> strings,
        IReadOnlyList<(string Cell, decimal Value)> numbers)
    {
        var cells = strings
            .Select(cell => (cell.Cell, Xml: $"<c r=\"{cell.Cell}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(cell.Value)}</t></is></c>"))
            .Concat(numbers.Select(cell => (cell.Cell, Xml: $"<c r=\"{cell.Cell}\"><v>{cell.Value}</v></c>")))
            .GroupBy(cell => RowNumber(cell.Cell))
            .OrderBy(group => group.Key)
            .Select(group => $"<row r=\"{group.Key}\">{string.Join("", group.OrderBy(cell => cell.Cell).Select(cell => cell.Xml))}</row>");

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>{string.Join("", cells)}</sheetData>
            </worksheet>
            """;
    }

    private static int RowNumber(string cell)
    {
        return int.Parse(new string(cell.Where(char.IsDigit).ToArray()));
    }
}
