namespace AssociationSaleTool.Web.Domain.Items;

/// <summary>
/// Thrown when a reference allocation would push an item number past the
/// three-digit format limit. Total capacity is 20 x 999 references against an
/// event of roughly 4,200 items, so this cannot occur in practice. The guard
/// exists only to prevent silent corruption of the reference format and of
/// reference sort order, and deliberately has no operator-facing recovery.
/// </summary>
public sealed class ReferenceSpaceExhaustedException : InvalidOperationException
{
    public ReferenceSpaceExhaustedException(char letter, int requestedLastNumber)
        : base($"Reference space exhausted for letter '{letter}': number {requestedLastNumber} exceeds 999.")
    {
        Letter = letter;
        RequestedLastNumber = requestedLastNumber;
    }

    public char Letter { get; }

    public int RequestedLastNumber { get; }
}
