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
        // toys out of the bag part-way through the deposit.
        if (existingLetter is { } existing && Fits(usedByLetter, existing, count, softCapacity))
        {
            return existing;
        }

        if (Fits(usedByLetter, ownLetter, count, softCapacity))
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

        // Relax: every letter is full, so the soft cap is ignored and the
        // seller goes into their own letter, onto labels printed on the day.
        return spill ?? ownLetter;
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
