namespace AssociationSaleTool.Web.Domain.Sellers;

public static class SellerCodeGenerator
{
    private static readonly char[] SafeAlphabet =
    [
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L',
        'M', 'N', 'P', 'R', 'S', 'T', 'U', 'W', 'X', 'Y', 'Z'
    ];

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
}
