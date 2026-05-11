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
        var (validRows, ignoredLines) = ParseItemRows(articles);
        if (ignoredLines.Count > 0)
        {
            importNotes.Add($"Import formulaire : {ignoredLines.Count} ligne{(ignoredLines.Count > 1 ? "s" : "")} article{(ignoredLines.Count > 1 ? "s" : "")} ignorée{(ignoredLines.Count > 1 ? "s" : "")}.");
            importNotes.AddRange(ignoredLines.Select(line => $"Ligne {line.LineNumber} : {line.Reason} - {line.RawValue}"));
        }

        if (validRows.Count == 0)
        {
            importNotes.Add("Import formulaire : aucun article valide importé.");
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
            validRows,
            ignoredLines);
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

    private static (IReadOnlyList<CreateItemRowRequest> ValidRows, IReadOnlyList<SellerFormIgnoredItemLine> IgnoredLines) ParseItemRows(string articles)
    {
        var validRows = new List<CreateItemRowRequest>();
        var ignoredLines = new List<SellerFormIgnoredItemLine>();
        var normalized = articles.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (TryParseItemLine(raw, out var row, out var reason))
            {
                validRows.Add(row);
            }
            else
            {
                ignoredLines.Add(new SellerFormIgnoredItemLine(index + 1, raw, reason));
            }
        }

        return (validRows, ignoredLines);
    }

    private static bool TryParseItemLine(string raw, out CreateItemRowRequest row, out string reason)
    {
        row = new CreateItemRowRequest(string.Empty, 0);
        reason = "prix manquant";

        if (TryParseWholeEuroPrice(raw, out _))
        {
            reason = "titre manquant";
            return false;
        }

        var match = ItemLinePattern().Match(raw);
        if (!match.Success)
        {
            return false;
        }

        var title = match.Groups["title"].Value.Trim().Trim(',', ';');
        var amount = match.Groups["amount"].Value.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            reason = "titre manquant";
            return false;
        }

        if (!TryParseWholeEuroPrice(amount, out var priceCents))
        {
            reason = "prix non accepté";
            return false;
        }

        row = new CreateItemRowRequest(title, priceCents);
        return true;
    }

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
}
