using AssociationSaleTool.Web.Domain.Sellers;

namespace AssociationSaleTool.Web.Tests.Domain;

public sealed class SellerFeeCalculatorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 200)]
    [InlineData(15, 200)]
    [InlineData(16, 400)]
    [InlineData(30, 400)]
    [InlineData(31, 600)]
    public void CalculateExpectedFeeCents_ChargesEveryStartedFifteenItemBlock(
        int itemCount,
        int expectedFeeCents)
    {
        var fee = SellerFeeCalculator.CalculateExpectedFeeCents(itemCount);

        Assert.Equal(expectedFeeCents, fee);
    }

    [Theory]
    [InlineData(400, 0, 400)]
    [InlineData(400, 200, 200)]
    [InlineData(400, 400, 0)]
    [InlineData(400, 600, 0)]
    public void CalculateRemainingFeeCents_NeverReturnsNegativeFee(
        int expectedFeeCents,
        int paidFeeCents,
        int remainingFeeCents)
    {
        var fee = SellerFeeCalculator.CalculateRemainingFeeCents(expectedFeeCents, paidFeeCents);

        Assert.Equal(remainingFeeCents, fee);
    }
}
