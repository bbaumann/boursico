using AssociationSaleTool.Web.Infrastructure;

namespace AssociationSaleTool.Web.Tests.Infrastructure;

public sealed class EuroPriceInputTests
{
    [Theory]
    [InlineData("4", 400)]
    [InlineData("4,00", 400)]
    [InlineData("10.00", 1000)]
    public void TryParseToCents_AcceptsWholeEuroAmounts(string rawValue, int expectedCents)
    {
        var parsed = EuroPriceInput.TryParseToCents(rawValue, out var cents);

        Assert.True(parsed);
        Assert.Equal(expectedCents, cents);
    }

    [Theory]
    [InlineData("4,50")]
    [InlineData("4.01")]
    [InlineData("4.555")]
    public void TryParseToCents_RejectsCentAmounts(string rawValue)
    {
        var parsed = EuroPriceInput.TryParseToCents(rawValue, out var cents);

        Assert.False(parsed);
        Assert.Equal(0, cents);
    }

    [Theory]
    [InlineData(1200, "12")]
    [InlineData(1250, "12.50")]
    public void FormatFromCents_UsesIntegerTextWhenPossible(int cents, string expected)
    {
        var formatted = EuroPriceInput.FormatFromCents(cents);

        Assert.Equal(expected, formatted);
    }
}
