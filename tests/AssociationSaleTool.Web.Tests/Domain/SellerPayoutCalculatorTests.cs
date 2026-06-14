using AssociationSaleTool.Web.Domain.Sellers;

namespace AssociationSaleTool.Web.Tests.Domain;

public sealed class SellerPayoutCalculatorTests
{
    [Theory]
    [InlineData(2000, 200, 300, 1500)]
    [InlineData(105, 0, 16, 89)]
    [InlineData(104, 0, 16, 88)]
    [InlineData(100, 100, 15, 0)]
    public void CalculateTotalPayableCents_DeductsAssociationMarginAndRemainingFees(
        int itemSubtotalCents,
        int remainingFeeCents,
        int expectedAssociationMarginCents,
        int expectedTotalPayableCents)
    {
        var associationMarginCents = SellerPayoutCalculator.CalculateAssociationMarginCents(itemSubtotalCents);
        var totalPayableCents = SellerPayoutCalculator.CalculateTotalPayableCents(itemSubtotalCents, remainingFeeCents);

        Assert.Equal(expectedAssociationMarginCents, associationMarginCents);
        Assert.Equal(expectedTotalPayableCents, totalPayableCents);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(100, -1)]
    public void CalculateTotalPayableCents_RejectsNegativeAmounts(int itemSubtotalCents, int remainingFeeCents)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SellerPayoutCalculator.CalculateTotalPayableCents(itemSubtotalCents, remainingFeeCents));
    }
}
