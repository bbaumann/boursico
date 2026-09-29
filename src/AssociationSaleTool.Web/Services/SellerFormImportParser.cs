using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualBasic.FileIO;

namespace AssociationSaleTool.Web.Services;

public sealed partial class SellerFormImportParser(IserePostalCityLookup? postalCityLookup = null)
{
    private readonly IserePostalCityLookup _postalCityLookup = postalCityLookup ?? IserePostalCityLookup.Empty;

    public Task<SellerFormImportParseResult> ParseAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return Task.FromResult(new SellerFormImportParseResult(false, [], ["Le fichier d'import est obligatoire."]));
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".tsv", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new SellerFormImportParseResult(false, [], ["Le fichier doit être au format .csv ou .tsv."]));
        }

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var parser = new TextFieldParser(reader)
        {
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(string.Equals(extension, ".tsv", StringComparison.OrdinalIgnoreCase) ? "\t" : ",");

        var records = new List<string[]>();
        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            records.Add(parser.ReadFields() ?? []);
        }

        var headerIndex = records.FindIndex(IsHeaderRow);
        if (headerIndex < 0)
        {
            return Task.FromResult(new SellerFormImportParseResult(false, [], ["La ligne d'en-tête du fichier est introuvable."]));
        }

        var headers = BuildHeaderIndex(records[headerIndex]);
        var submissions = new List<SellerFormImportSubmission>();
        for (var index = headerIndex + 1; index < records.Count; index++)
        {
            var record = records[index];
            if (record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            submissions.Add(BuildSubmission(record, headers, index + 1));
        }

        return Task.FromResult(new SellerFormImportParseResult(true, submissions, []));
    }

    private SellerFormImportSubmission BuildSubmission(
        string[] record,
        IReadOnlyDictionary<string, int> headers,
        int sourceRowNumber)
    {
        var firstName = NormalizeText(ReadField(record, headers, "prenom")) ?? string.Empty;
        var lastName = NormalizeText(ReadField(record, headers, "nom")) ?? string.Empty;
        var phone = NormalizeText(ReadField(record, headers, "telephone"));
        var email = NormalizeText(ReadField(record, headers, "email"));
        var postalAddress = NormalizeText(ReadField(record, headers, "adresse")) ?? string.Empty;
        var postalCode = NormalizeText(ReadField(record, headers, "codepostal")) ?? string.Empty;
        var submittedCity = NormalizeText(ReadField(record, headers, "ville"));
        var notes = NormalizeText(ReadField(record, headers, "notes"));
        var articles = ReadField(record, headers, "jouetprix") ?? string.Empty;

        var importNotes = new List<string>();
        var city = NormalizeCity(postalCode, submittedCity, importNotes);
        var (itemRows, ignoredLines, incompleteLines) = ParseItemRows(articles);
        if (incompleteLines.Count > 0)
        {
            importNotes.Add($"Import formulaire : {incompleteLines.Count} article{(incompleteLines.Count > 1 ? "s" : "")} à corriger importé{(incompleteLines.Count > 1 ? "s" : "")} sans prix.");
            importNotes.AddRange(incompleteLines.Select(line => $"Ligne {line.LineNumber} : {line.Reason} - {line.RawValue}"));
        }

        if (ignoredLines.Count > 0)
        {
            importNotes.Add($"Import formulaire : {ignoredLines.Count} ligne{(ignoredLines.Count > 1 ? "s" : "")} article{(ignoredLines.Count > 1 ? "s" : "")} non importée{(ignoredLines.Count > 1 ? "s" : "")}.");
            importNotes.AddRange(ignoredLines.Select(line => $"Ligne {line.LineNumber} : {line.Reason} - {line.RawValue}"));
        }

        if (itemRows.Count == 0)
        {
            importNotes.Add("Import formulaire : aucun article importé.");
        }

        return new SellerFormImportSubmission(
            sourceRowNumber,
            firstName,
            lastName,
            email,
            phone,
            postalAddress,
            postalCode,
            city,
            notes,
            JoinImportNotes(importNotes),
            itemRows,
            ignoredLines,
            incompleteLines);
    }

    private string NormalizeCity(string postalCode, string? submittedCity, List<string> importNotes)
    {
        if (string.IsNullOrWhiteSpace(submittedCity))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            importNotes.Add($"Import formulaire : ville reçue sans code postal : \"{submittedCity}\".");
            return string.Empty;
        }

        if (_postalCityLookup.TryFindCanonicalCity(postalCode, submittedCity, out var exactCity)
            || _postalCityLookup.TryFindCanonicalCityLenient(postalCode, submittedCity, out exactCity))
        {
            return exactCity ?? string.Empty;
        }

        importNotes.Add($"Import formulaire : ville reçue non reconnue pour le code postal {postalCode} : \"{submittedCity}\".");
        return string.Empty;
    }

    private static (
        IReadOnlyList<CreateItemRowRequest> ItemRows,
        IReadOnlyList<SellerFormIgnoredItemLine> IgnoredLines,
        IReadOnlyList<SellerFormIncompleteItemLine> IncompleteLines) ParseItemRows(string articles)
    {
        var itemRows = new List<CreateItemRowRequest>();
        var ignoredLines = new List<SellerFormIgnoredItemLine>();
        var incompleteLines = new List<SellerFormIncompleteItemLine>();
        var normalized = articles.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var parsedLine = ParseItemLine(raw);
            if (parsedLine.Row is not null)
            {
                itemRows.Add(parsedLine.Row);
                if (parsedLine.Row.SellerPriceCents is null)
                {
                    incompleteLines.Add(new SellerFormIncompleteItemLine(
                        index + 1,
                        raw,
                        parsedLine.Reason,
                        parsedLine.Row.Title));
                }
            }
            else
            {
                ignoredLines.Add(new SellerFormIgnoredItemLine(index + 1, raw, parsedLine.Reason));
            }
        }

        return (itemRows, ignoredLines, incompleteLines);
    }

    private static ParsedItemLine ParseItemLine(string raw)
    {
        if (PriceLikeOnlyPattern().IsMatch(raw))
        {
            return new ParsedItemLine(null, "description manquante");
        }

        var match = ItemLinePattern().Match(raw);
        if (match.Success)
        {
            var matchedTitle = NormalizeItemTitle(match.Groups["title"].Value);
            var amount = match.Groups["amount"].Value.Trim();
            if (string.IsNullOrWhiteSpace(matchedTitle))
            {
                return new ParsedItemLine(null, "description manquante");
            }

            if (!TryParseWholeEuroPrice(amount, out var priceCents))
            {
                return new ParsedItemLine(new CreateItemRowRequest(matchedTitle, null), "prix non accepté");
            }

            return new ParsedItemLine(new CreateItemRowRequest(matchedTitle, priceCents), string.Empty);
        }

        var title = NormalizeItemTitle(raw);
        if (string.IsNullOrWhiteSpace(title))
        {
            return new ParsedItemLine(null, "description manquante");
        }

        return new ParsedItemLine(new CreateItemRowRequest(title, null), "prix manquant");
    }

    private static string NormalizeItemTitle(string value)
    {
        return value.Trim().Trim(',', ';');
    }

    private sealed record ParsedItemLine(CreateItemRowRequest? Row, string Reason);

    private static bool TryParseWholeEuroPrice(string value, out int priceCents)
    {
        priceCents = 0;
        var trimmed = value.Trim();
        var priceMatch = PriceOnlyPattern().Match(trimmed);
        if (!priceMatch.Success)
        {
            return false;
        }

        var euros = int.Parse(priceMatch.Groups["euros"].Value, CultureInfo.InvariantCulture);
        var decimals = priceMatch.Groups["decimals"].Value;
        if (!string.IsNullOrEmpty(decimals) && decimals != "00")
        {
            return false;
        }

        priceCents = euros * 100;
        return true;
    }

    private static string? JoinImportNotes(IEnumerable<string> notes)
    {
        var normalized = notes
            .Select(note => note.Trim())
            .Where(note => !string.IsNullOrWhiteSpace(note))
            .ToList();

        return normalized.Count == 0 ? null : string.Join(Environment.NewLine, normalized);
    }

    private static bool IsHeaderRow(string[] row)
    {
        var normalized = row.Select(NormalizeHeader).ToHashSet(StringComparer.Ordinal);
        return normalized.Contains("nom")
            && normalized.Contains("prenom")
            && normalized.Contains("jouetprix");
    }

    private static Dictionary<string, int> BuildHeaderIndex(string[] row)
    {
        var headers = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < row.Length; index++)
        {
            var normalized = NormalizeHeader(row[index]);
            if (!string.IsNullOrWhiteSpace(normalized) && !headers.ContainsKey(normalized))
            {
                headers.Add(normalized, index);
            }
        }

        return headers;
    }

    private static string? ReadField(string[] record, IReadOnlyDictionary<string, int> headers, string name)
    {
        return headers.TryGetValue(name, out var index) && index < record.Length
            ? record[index]
            : null;
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var upper = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(upper.Length);
        foreach (var character in upper)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex("^(?<title>.+?)(?:\\s*[,;]\\s*|\\s+)(?<amount>\\d+(?:[,.]\\d{1,2})?)(?:\\s*(?:€|EUR))?\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ItemLinePattern();

    [GeneratedRegex("^(?<euros>\\d+)(?:[,.](?<decimals>\\d{2}))?(?:\\s*(?:€|EUR))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PriceOnlyPattern();

    [GeneratedRegex("^\\d+(?:[,.]\\d{1,2})?(?:\\s*(?:€|EUR))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PriceLikeOnlyPattern();
}
