namespace AssociationSaleTool.Web.Domain.Items;

/// <summary>
/// Decides which letter bucket a seller's contiguous reference run goes into.
/// The letter is a physical sort bucket for post-sale recollection, not a
/// seller identifier: twenty letters means twenty piles that twenty volunteers
/// can sort in parallel, so keeping them balanced is the point.
/// </summary>
public static class ReferenceAllocator
{
    public const int HardCap = 999;

    // Same alphabet as SellerCodeGenerator: 'A' is reserved for the
    // association, and F, I, O, Q, V are excluded as visually confusable.
    public static readonly IReadOnlyList<char> SafeAlphabet =
    [
        'B', 'C', 'D', 'E', 'G', 'H', 'J', 'K', 'L',
        'M', 'N', 'P', 'R', 'S', 'T', 'U', 'W', 'X', 'Y', 'Z'
    ];

    public static char ChooseLetter(
        IReadOnlyDictionary<char, int> usedByLetter,
        char ownLetter,
        char? existingLetter,
        int count,
        int softCapacity)
    {
        ArgumentNullException.ThrowIfNull(usedByLetter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var chosen = SelectLetter(usedByLetter, ownLetter, existingLetter, count, softCapacity);

        var lastNumber = Used(usedByLetter, chosen) + count;
        if (lastNumber > HardCap)
        {
            throw new ReferenceSpaceExhaustedException(chosen, lastNumber);
        }

        return chosen;
    }

    private static char SelectLetter(
        IReadOnlyDictionary<char, int> usedByLetter,
        char ownLetter,
        char? existingLetter,
        int count,
        int softCapacity)
    {
        // Top-up: keep the seller's items in one sort pile when they pull more
        // toys out of the bag part-way through the deposit. A stale letter -
        // e.g. the 'A' from a pre-migration "AA-001" reference - is not a
        // real bucket and must not be trusted, so it falls through instead.
        if (existingLetter is { } existing
            && IsSafe(existing)
            && Fits(usedByLetter, existing, count, softCapacity))
        {
            return existing;
        }

        var ownLetterIsSafe = IsSafe(ownLetter);
        if (ownLetterIsSafe && Fits(usedByLetter, ownLetter, count, softCapacity))
        {
            return ownLetter;
        }

        // Spill: the least-loaded letter with room, which keeps the piles even.
        // Alphabet order breaks ties so the choice is deterministic. Ordering
        // by the position in SafeAlphabet rather than by the character itself
        // keeps the tie-break tied to the declared alphabet order.
        var spill = SafeAlphabet
            .Select((letter, index) => (Letter: letter, Index: index))
            .Where(candidate => Fits(usedByLetter, candidate.Letter, count, softCapacity))
            .OrderBy(candidate => Used(usedByLetter, candidate.Letter))
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => (char?)candidate.Letter)
            .FirstOrDefault();

        if (spill is { } spillLetter)
        {
            return spillLetter;
        }

        // Relax: every letter is full, so the soft cap is ignored and the
        // seller goes into their own letter, onto labels printed on the day.
        // If ownLetter itself is not a real bucket (a stale/hand-edited
        // seller code), that letter cannot be used either: fall back to the
        // least-loaded real bucket regardless of the soft cap, so the result
        // is always one of the twenty printed letters.
        if (ownLetterIsSafe)
        {
            return ownLetter;
        }

        return SafeAlphabet
            .Select((letter, index) => (Letter: letter, Index: index))
            .OrderBy(candidate => Used(usedByLetter, candidate.Letter))
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => candidate.Letter)
            .First();
    }

    private static bool IsSafe(char letter)
    {
        return SafeAlphabet.Contains(letter);
    }

    private static bool Fits(
        IReadOnlyDictionary<char, int> usedByLetter,
        char letter,
        int count,
        int softCapacity)
    {
        return Used(usedByLetter, letter) + count <= softCapacity;
    }

    private static int Used(IReadOnlyDictionary<char, int> usedByLetter, char letter)
    {
        return usedByLetter.TryGetValue(letter, out var used) ? used : 0;
    }
}
