using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Http;

namespace AssociationSaleTool.Web.Services;

public sealed class ExcelSellerItemParser
{
    private const string DefaultPostalAddress = "";
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace OfficeRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private readonly IserePostalCityLookup postalCityLookup;

    public ExcelSellerItemParser(IserePostalCityLookup? postalCityLookup = null)
    {
        this.postalCityLookup = postalCityLookup ?? IserePostalCityLookup.Empty;
    }

    public async Task<ExcelSellerItemParseResult> ParseAsync(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return ExcelSellerItemParseResult.Failure("Sélectionnez un fichier Excel.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return ExcelSellerItemParseResult.Failure("Le fichier doit être au format .xlsx.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            return ParseArchive(archive);
        }
        catch (InvalidDataException)
        {
            return ExcelSellerItemParseResult.Failure("Le fichier Excel ne peut pas être lu.");
        }
    }

    private ExcelSellerItemParseResult ParseArchive(ZipArchive archive)
    {
        var workbook = LoadXml(archive, "xl/workbook.xml");
        if (workbook is null)
        {
            return ExcelSellerItemParseResult.Failure("Le fichier Excel ne contient pas de classeur lisible.");
        }

        var sheets = workbook
            .Descendants(SpreadsheetNamespace + "sheet")
            .Where(IsVisibleSheet)
            .ToList();
        if (sheets.Count != 1)
        {
            return ExcelSellerItemParseResult.Failure("Le fichier Excel doit contenir une seule feuille vendeur.");
        }

        var relationshipId = (string?)sheets[0].Attribute(OfficeRelationshipsNamespace + "id");
        var worksheetPath = GetWorksheetPath(archive, relationshipId);
        if (worksheetPath is null)
        {
            return ExcelSellerItemParseResult.Failure("La feuille vendeur est introuvable dans le fichier Excel.");
        }

        var worksheet = LoadXml(archive, worksheetPath);
        if (worksheet is null)
        {
            return ExcelSellerItemParseResult.Failure("La feuille vendeur ne peut pas être lue.");
        }

        var sharedStrings = LoadSharedStrings(archive);
        var cells = ReadCells(worksheet, sharedStrings);
        var errors = new List<string>();

        var rawSellerName = FindValueNearLabel(cells, ["NOM PRENOM", "NOM"]);
        if (!TrySplitSellerName(rawSellerName, out var firstName, out var lastName))
        {
            errors.Add("Le nom du vendeur est introuvable dans le fichier Excel.");
        }

        var phone = NormalizePhoneCell(FindValueNearLabel(cells, ["TELEPHONE", "TÉLÉPHONE"]));
        if (!TryReadPostalCodeAndCity(
                FindValueNearLabel(cells, ["CP VILLE", "CODE POSTAL VILLE"]),
                out var postalCode,
                out var city,
                out var postalCityError))
        {
            errors.Add(postalCityError!);
        }

        if (!TryFindItemColumns(cells, out var titleColumn, out var priceColumn))
        {
            errors.Add("Les colonnes des articles sont introuvables dans le fichier Excel.");
        }

        var rows = titleColumn is null || priceColumn is null
            ? []
            : ReadItemRows(cells, titleColumn, priceColumn, errors);

        if (rows.Count == 0 && errors.Count == 0)
        {
            errors.Add("Aucune ligne d'article exploitable n'a été trouvée dans le fichier Excel.");
        }

        if (errors.Count > 0)
        {
            return ExcelSellerItemParseResult.Failure(errors);
        }

        return ExcelSellerItemParseResult.Success(
            firstName!,
            lastName!,
            null,
            phone,
            DefaultPostalAddress,
            postalCode,
            city,
            null,
            IntakeMode.EmailPreregistration,
            rows);
    }

    private static bool IsVisibleSheet(XElement sheet)
    {
        var state = (string?)sheet.Attribute("state");
        return string.IsNullOrWhiteSpace(state)
            || string.Equals(state, "visible", StringComparison.OrdinalIgnoreCase);
    }

    private static XDocument? LoadXml(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static string? GetWorksheetPath(ZipArchive archive, string? relationshipId)
    {
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            return null;
        }

        var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        var target = relationships?
            .Descendants(RelationshipsNamespace + "Relationship")
            .Where(relationship => string.Equals((string?)relationship.Attribute("Id"), relationshipId, StringComparison.Ordinal))
            .Select(relationship => (string?)relationship.Attribute("Target"))
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        if (target.StartsWith('/'))
        {
            return target.TrimStart('/');
        }

        return target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? target
            : $"xl/{target}";
    }

    private static List<string> LoadSharedStrings(ZipArchive archive)
    {
        var sharedStrings = LoadXml(archive, "xl/sharedStrings.xml");
        if (sharedStrings is null)
        {
            return [];
        }

        return sharedStrings
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
            .ToList();
    }

    private static Dictionary<string, string> ReadCells(XDocument worksheet, IReadOnlyList<string> sharedStrings)
    {
        var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in worksheet.Descendants(SpreadsheetNamespace + "c"))
        {
            var reference = (string?)cell.Attribute("r");
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            var text = ReadCellText(cell, sharedStrings);
            if (!string.IsNullOrWhiteSpace(text))
            {
                cells[reference] = text.Trim();
            }
        }

        return cells;
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
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        return value;
    }

    private static string? FindValueNearLabel(Dictionary<string, string> cells, IReadOnlyList<string> labels)
    {
        foreach (var cell in cells)
        {
            if (!labels.Any(label => NormalizeForLookup(cell.Value) == NormalizeForLookup(label)))
            {
                continue;
            }

            var (column, row) = SplitReference(cell.Key);
            var below = GetCell(cells, column, row + 1);
            if (!IsPlaceholder(below))
            {
                return below;
            }

            for (var offset = 1; offset <= 2; offset++)
            {
                var right = GetCell(cells, NumberToColumn(ColumnToNumber(column) + offset), row);
                if (!IsPlaceholder(right))
                {
                    return right;
                }
            }
        }

        return null;
    }

    private static bool TryFindItemColumns(
        Dictionary<string, string> cells,
        out string? titleColumn,
        out string? priceColumn)
    {
        foreach (var cell in cells)
        {
            if (NormalizeForLookup(cell.Value) != "LIBELLE")
            {
                continue;
            }

            var (column, row) = SplitReference(cell.Key);
            var nextColumn = NumberToColumn(ColumnToNumber(column) + 1);
            if (NormalizeForLookup(GetCell(cells, nextColumn, row)) == "PRIX")
            {
                titleColumn = column;
                priceColumn = nextColumn;
                return true;
            }
        }

        titleColumn = null;
        priceColumn = null;
        return false;
    }

    private static List<CreateItemRowRequest> ReadItemRows(
        Dictionary<string, string> cells,
        string titleColumn,
        string priceColumn,
        List<string> errors)
    {
        var rows = new List<CreateItemRowRequest>();
        var maxRow = cells.Keys.Select(reference => SplitReference(reference).Row).DefaultIfEmpty(7).Max();

        for (var rowNumber = 7; rowNumber <= maxRow + 1; rowNumber++)
        {
            var title = NormalizeOptionalCell(GetCell(cells, titleColumn, rowNumber));
            var price = NormalizeOptionalCell(GetCell(cells, priceColumn, rowNumber));

            if (title is null && price is null)
            {
                break;
            }

            if (title is null)
            {
                errors.Add($"La ligne {rowNumber} contient un prix vendeur sans description.");
                continue;
            }

            if (price is null)
            {
                errors.Add($"La ligne {rowNumber} contient une description sans prix vendeur.");
                continue;
            }

            if (!EuroPriceInput.TryParseToCents(price, out var sellerPriceCents))
            {
                errors.Add($"La ligne {rowNumber} contient un prix vendeur invalide.");
                continue;
            }

            rows.Add(new CreateItemRowRequest(title, sellerPriceCents));
        }

        return rows;
    }

    private static bool TrySplitSellerName(string? rawSellerName, out string? firstName, out string? lastName)
    {
        firstName = null;
        lastName = null;

        var normalized = NormalizeOptionalCell(rawSellerName);
        if (normalized is null)
        {
            return false;
        }

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        lastName = ToDisplayNamePart(parts[0]);
        firstName = ToDisplayNamePart(string.Join(' ', parts.Skip(1)));
        return true;
    }

    private static string ToDisplayNamePart(string value)
    {
        return FrenchCulture.TextInfo.ToTitleCase(value.ToLower(FrenchCulture));
    }

    private static string? NormalizeOptionalCell(string? value)
    {
        return IsPlaceholder(value) ? null : value!.Trim();
    }

    private static string? NormalizePhoneCell(string? value)
    {
        var phone = NormalizeOptionalCell(value);
        if (phone is null)
        {
            return null;
        }

        if (!phone.StartsWith('0') && phone.All(char.IsDigit))
        {
            return $"0{phone}";
        }

        return phone;
    }

    private bool TryReadPostalCodeAndCity(
        string? rawPostalCity,
        out string? postalCode,
        out string? city,
        out string? error)
    {
        postalCode = null;
        city = null;
        error = null;

        var value = NormalizeOptionalCell(rawPostalCity);
        if (value is null)
        {
            return true;
        }

        var match = Regex.Match(value, "^(?<postalCode>\\d{5})\\s+(?<city>.+)$");
        if (!match.Success)
        {
            error = "Le code postal et la ville du fichier Excel sont illisibles.";
            return false;
        }

        var parsedPostalCode = match.Groups["postalCode"].Value;
        var parsedCity = match.Groups["city"].Value.Trim();
        if (!postalCityLookup.TryFindCanonicalCity(parsedPostalCode, parsedCity, out var canonicalCity))
        {
            error = $"La ville \"{parsedCity}\" n'est pas connue pour le code postal {parsedPostalCode} dans la liste officielle de l'Isère.";
            return false;
        }

        postalCode = parsedPostalCode;
        city = canonicalCity;
        return true;
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = NormalizeForLookup(value);
        return normalized is "A SAISIR" or "RENSEIGNER";
    }

    private static string? GetCell(Dictionary<string, string> cells, string column, int row)
    {
        return cells.TryGetValue($"{column}{row}", out var value) ? value : null;
    }

    private static (string Column, int Row) SplitReference(string reference)
    {
        var columnBuilder = new StringBuilder();
        var rowBuilder = new StringBuilder();

        foreach (var character in reference)
        {
            if (char.IsLetter(character))
            {
                columnBuilder.Append(char.ToUpperInvariant(character));
            }
            else if (char.IsDigit(character))
            {
                rowBuilder.Append(character);
            }
        }

        return (columnBuilder.ToString(), int.Parse(rowBuilder.ToString(), CultureInfo.InvariantCulture));
    }

    private static int ColumnToNumber(string column)
    {
        var result = 0;
        foreach (var character in column)
        {
            result *= 26;
            result += character - 'A' + 1;
        }

        return result;
    }

    private static string NumberToColumn(int number)
    {
        var builder = new StringBuilder();
        while (number > 0)
        {
            number--;
            builder.Insert(0, (char)('A' + number % 26));
            number /= 26;
        }

        return builder.ToString();
    }

    private static string NormalizeForLookup(string? value)
    {
        var upper = string.Join(
                ' ',
                (value ?? string.Empty).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant()
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(upper.Length);

        foreach (var character in upper)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
