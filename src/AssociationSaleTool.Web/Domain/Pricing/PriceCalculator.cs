namespace AssociationSaleTool.Web.Domain.Pricing;

public static class PriceCalculator
{
    public static int ToSalePriceCents(int sellerPriceCents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sellerPriceCents);
        return sellerPriceCents + (sellerPriceCents / 5);
    }
}
