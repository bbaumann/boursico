namespace AssociationSaleTool.Web.Domain.Sellers;

public static class SellerPayoutCalculator
{
    public const int AssociationMarginPercent = 15;

    public static int CalculateAssociationMarginCents(int itemSubtotalCents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemSubtotalCents);

        return (int)Math.Round(
            itemSubtotalCents * AssociationMarginPercent / 100m,
            0,
            MidpointRounding.AwayFromZero);
    }

    public static int CalculateTotalPayableCents(int itemSubtotalCents, int remainingFeeCents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemSubtotalCents);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingFeeCents);

        var associationMarginCents = CalculateAssociationMarginCents(itemSubtotalCents);
        return Math.Max(itemSubtotalCents - associationMarginCents - remainingFeeCents, 0);
    }
}
