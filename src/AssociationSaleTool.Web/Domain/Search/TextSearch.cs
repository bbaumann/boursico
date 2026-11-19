using System.Globalization;
using System.Text;

namespace AssociationSaleTool.Web.Domain.Search;

/// <summary>
/// Word-based text search: an entry matches when each word of the query appears
/// somewhere in its text, in any order. Case, accents and punctuation are ignored,
/// filler words ("de", "la", "avec"…) are dropped and plurals are reduced, so
/// "camions de pompiers" finds "Camion pompier". Mirrors the search helpers at the
/// top of wwwroot/js/site.js; keep both in sync.
/// </summary>
public sealed class TextSearch
{
    private static readonly HashSet<string> StopWords =
    [
        "a", "au", "aux", "avec", "d", "de", "des", "du", "en", "et",
        "l", "la", "le", "les", "ou", "par", "pour", "sur", "un", "une"
    ];

    private readonly string[] words;

    private TextSearch(string[] words)
    {
        this.words = words;
    }

    public bool IsEmpty => words.Length == 0;

    public static TextSearch Parse(string? query)
    {
        var allWords = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var meaningfulWords = allWords.Where(word => !StopWords.Contains(word)).ToArray();
        // A query made only of short words ("le", while typing "lego") is kept as typed.
        var kept = meaningfulWords.Length > 0 ? meaningfulWords : allWords;
        return new TextSearch(kept.Select(Stem).ToArray());
    }

    public bool Matches(params string?[] fields)
    {
        if (IsEmpty)
        {
            return true;
        }

        var text = Normalize(string.Join(' ', fields));
        return words.All(word => text.Contains(word, StringComparison.Ordinal));
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decomposed = value
            .ToLowerInvariant()
            .Replace("œ", "oe", StringComparison.Ordinal)
            .Replace("æ", "ae", StringComparison.Ordinal)
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(character);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    private static string Stem(string word)
    {
        if (!word.All(character => character is >= 'a' and <= 'z'))
        {
            return word;
        }

        // "chevaux" -> "cheva", which is contained in both "cheval" and "chevaux".
        if (word.Length >= 5 && word.EndsWith("aux", StringComparison.Ordinal))
        {
            return word[..^2];
        }

        if (word.Length >= 4 && (word.EndsWith('s') || word.EndsWith('x')))
        {
            return word[..^1];
        }

        return word;
    }
}
