using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AssociationSaleTool.Web.Services;

public sealed class IserePostalCityLookup
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> citiesByPostalCode;

    public static IserePostalCityLookup Empty { get; } = new(
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    public IserePostalCityLookup(IReadOnlyDictionary<string, IReadOnlyList<string>> citiesByPostalCode)
    {
        this.citiesByPostalCode = citiesByPostalCode
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);
    }

    public static IserePostalCityLookup LoadFromJsonFile(string jsonPath)
    {
        using var stream = File.OpenRead(jsonPath);
        var cities = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)
            ?? new Dictionary<string, string[]>(StringComparer.Ordinal);

        return new IserePostalCityLookup(
            cities.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value,
                StringComparer.Ordinal));
    }

    public bool TryFindCanonicalCity(string? postalCode, string? city, out string? canonicalCity)
    {
        canonicalCity = null;

        var normalizedPostalCode = postalCode?.Trim();
        var normalizedCity = city?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPostalCode) || string.IsNullOrWhiteSpace(normalizedCity))
        {
            return false;
        }

        if (!citiesByPostalCode.TryGetValue(normalizedPostalCode, out var cities))
        {
            return false;
        }

        foreach (var candidate in cities)
        {
            if (string.Equals(candidate, normalizedCity, StringComparison.Ordinal))
            {
                canonicalCity = candidate;
                return true;
            }
        }

        return false;
    }

    public bool HasCitiesForPostalCode(string? postalCode)
    {
        var normalizedPostalCode = postalCode?.Trim();
        return !string.IsNullOrWhiteSpace(normalizedPostalCode)
            && citiesByPostalCode.ContainsKey(normalizedPostalCode);
    }

    public bool TryFindCanonicalCityLenient(string? postalCode, string? city, out string? canonicalCity)
    {
        canonicalCity = null;

        var normalizedPostalCode = postalCode?.Trim();
        var normalizedCity = NormalizeCityForMatching(city);
        if (string.IsNullOrWhiteSpace(normalizedPostalCode) || string.IsNullOrWhiteSpace(normalizedCity))
        {
            return false;
        }

        if (!citiesByPostalCode.TryGetValue(normalizedPostalCode, out var cities))
        {
            return false;
        }

        foreach (var candidate in cities)
        {
            if (NormalizeCityForMatching(candidate) == normalizedCity)
            {
                canonicalCity = candidate;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeCityForMatching(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var upper = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(upper.Length);
        foreach (var character in upper)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        var tokens = builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token switch
            {
                "ST" => "SAINT",
                "STE" => "SAINTE",
                _ => token
            });

        return string.Join(' ', tokens);
    }
}
