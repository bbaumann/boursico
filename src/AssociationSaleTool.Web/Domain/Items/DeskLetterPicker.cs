namespace AssociationSaleTool.Web.Domain.Items;

public sealed record LetterState(char Letter, int LastNumber, long? HeldByDeskId);

public sealed record LetterSegment(char Letter, int Count);

public sealed record RunPlan(IReadOnlyList<LetterSegment> Segments, IReadOnlyList<char> LettersToTake);

/// <summary>
/// Decides which reference letters a deposit desk draws numbers from. A
/// letter is held by at most one open desk, so the desk's next number is
/// always the next label on the sheet in front of the operator.
/// </summary>
public static class DeskLetterPicker
{
    // Same alphabet as SellerCodeGenerator: 'A' is reserved for the
    // association, and F, I, O, Q, V are excluded as visually confusable.
    public static readonly IReadOnlyList<char> SafeAlphabet =
    [
        'B', 'C', 'D', 'E', 'G', 'H', 'J', 'K', 'L',
        'M', 'N', 'P', 'R', 'S', 'T', 'U', 'W', 'X', 'Y', 'Z'
    ];

    private static readonly Dictionary<char, int> AlphabetIndexes = SafeAlphabet
        .Select((letter, index) => (letter, index))
        .ToDictionary(x => x.letter, x => x.index);

    public static int AlphabetIndex(char letter)
    {
        return AlphabetIndexes.TryGetValue(letter, out var index) ? index : int.MaxValue;
    }

    public static int Room(LetterState letter, int labelsPerLetter)
    {
        return Math.Max(0, labelsPerLetter - letter.LastNumber);
    }

    public static IReadOnlyList<char> PickFreeLetters(
        IReadOnlyList<LetterState> letters,
        int count,
        int labelsPerLetter)
    {
        if (count <= 0)
        {
            return [];
        }

        return FreeLettersLeastUsedFirst(letters, labelsPerLetter)
            .Take(count)
            .Select(letter => letter.Letter)
            .ToList();
    }

    public static RunPlan? PlanRun(
        IReadOnlyList<LetterState> letters,
        long deskId,
        char? sellerLetter,
        int count,
        int labelsPerLetter)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var usable = letters
            .Where(letter => letter.HeldByDeskId == deskId && Room(letter, labelsPerLetter) > 0)
            .OrderByDescending(letter => Room(letter, labelsPerLetter))
            .ThenBy(letter => AlphabetIndex(letter.Letter))
            .ToList();
        var sellerUsable = sellerLetter is { } wanted
            ? usable.FirstOrDefault(letter => letter.Letter == wanted)
            : null;

        // Tier 1: keep a seller's extras next to their run.
        if (sellerUsable is not null && Room(sellerUsable, labelsPerLetter) >= count)
        {
            return new RunPlan([new LetterSegment(sellerUsable.Letter, count)], []);
        }

        // Tier 2: never split a run while one held letter can take all of it.
        if (usable.FirstOrDefault() is { } widest && Room(widest, labelsPerLetter) >= count)
        {
            return new RunPlan([new LetterSegment(widest.Letter, count)], []);
        }

        // Tier 3: split - seller letter, other held letters, then free letters taken on the spot.
        var candidates = new List<(LetterState Letter, bool Take)>();
        if (sellerUsable is not null)
        {
            candidates.Add((sellerUsable, false));
        }

        candidates.AddRange(usable
            .Where(letter => letter.Letter != sellerUsable?.Letter)
            .Select(letter => (letter, false)));
        candidates.AddRange(FreeLettersLeastUsedFirst(letters, labelsPerLetter)
            .Select(letter => (letter, true)));

        var segments = new List<LetterSegment>();
        var lettersToTake = new List<char>();
        var remaining = count;
        foreach (var (letter, take) in candidates)
        {
            if (remaining == 0)
            {
                break;
            }

            var used = Math.Min(remaining, Room(letter, labelsPerLetter));
            segments.Add(new LetterSegment(letter.Letter, used));
            if (take)
            {
                lettersToTake.Add(letter.Letter);
            }

            remaining -= used;
        }

        // Tier 4: no room at all.
        return remaining == 0 ? new RunPlan(segments, lettersToTake) : null;
    }

    private static IEnumerable<LetterState> FreeLettersLeastUsedFirst(
        IReadOnlyList<LetterState> letters,
        int labelsPerLetter)
    {
        return letters
            .Where(letter => letter.HeldByDeskId is null && Room(letter, labelsPerLetter) > 0)
            .OrderBy(letter => letter.LastNumber)
            .ThenBy(letter => AlphabetIndex(letter.Letter));
    }
}
