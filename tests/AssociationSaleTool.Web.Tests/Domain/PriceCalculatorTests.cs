using AssociationSaleTool.Web.Domain.Pricing;

namespace AssociationSaleTool.Web.Tests.Domain;

public class PriceCalculatorTests
{
    [Fact]
    public void AppliesTwentyPercentMarginToSellerPrice()
    {
        Assert.Equal(600, PriceCalculator.ToSalePriceCents(500));
    }
}
