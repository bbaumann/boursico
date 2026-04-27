namespace AssociationSaleTool.Web.Domain.Pricing;

public static class CheckoutPricingCalculator
{
    public static CheckoutTotals CalculateTotals(int subtotalSellerPriceCents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(subtotalSellerPriceCents);

        var checkoutFeeCents = (int)Math.Round(
            subtotalSellerPriceCents / 10m,
            0,
            MidpointRounding.AwayFromZero);

        return new CheckoutTotals(
            subtotalSellerPriceCents,
            checkoutFeeCents,
            subtotalSellerPriceCents + checkoutFeeCents);
    }
}
