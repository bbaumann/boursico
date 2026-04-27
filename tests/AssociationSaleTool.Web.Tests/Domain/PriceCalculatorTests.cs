using AssociationSaleTool.Web.Domain.Pricing;

namespace AssociationSaleTool.Web.Tests.Domain;

public class CheckoutPricingCalculatorTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(500, 50, 550)]
    [InlineData(999, 100, 1099)]
    [InlineData(105, 11, 116)]
    [InlineData(104, 10, 114)]
    public void CalculateTotals_AppliesTenPercentSaleLevelFee(int subtotalCents, int expectedFeeCents, int expectedTotalCents)
    {
        var totals = CheckoutPricingCalculator.CalculateTotals(subtotalCents);

        Assert.Equal(subtotalCents, totals.SubtotalSellerPriceCents);
        Assert.Equal(expectedFeeCents, totals.CheckoutFeeCents);
        Assert.Equal(expectedTotalCents, totals.TotalPaidCents);
    }
}
