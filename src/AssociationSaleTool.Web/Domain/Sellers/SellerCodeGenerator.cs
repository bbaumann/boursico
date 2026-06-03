namespace AssociationSaleTool.Web.Domain.Sellers;

public static class SellerCodeGenerator
{
    //A is reserved for Association Familiale, F,I,O,Q,V are removed because similar to other letters.numbers
    private static readonly char[] SafeAlphabet =
    [
        'B', 'C', 'D', 'E', 'G', 'H', 'J', 'K', 'L',
        'M', 'N', 'P', 'R', 'S', 'T', 'U', 'W', 'X', 'Y', 'Z'
    ];

    private static int GeneratedCodeCapacity => SafeAlphabet.Length * SafeAlphabet.Length;

    public static string GetCodeAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var first = SafeAlphabet[index % SafeAlphabet.Length];
        var secondIndex = index / SafeAlphabet.Length;

        if (secondIndex >= SafeAlphabet.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Seller code space exhausted for the safe alphabet.");
        }

        var second = SafeAlphabet[secondIndex];

        return string.Create(2, (first, second), static (span, state) =>
        {
            span[0] = state.first;
            span[1] = state.second;
        });
    }

    public static string GetNextAvailableCode(IEnumerable<string> existingCodes)
    {
        ArgumentNullException.ThrowIfNull(existingCodes);

        var usedCodes = existingCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < GeneratedCodeCapacity; index++)
        {
            var code = GetCodeAt(index);
            if (!usedCodes.Contains(code))
            {
                return code;
            }
        }

        for (var suffix = 1; ; suffix++)
        {
            for (var index = 0; index < GeneratedCodeCapacity; index++)
            {
                var code = $"{GetCodeAt(index)}{suffix}";
                if (!usedCodes.Contains(code))
                {
                    return code;
                }
            }
        }
    }
}
