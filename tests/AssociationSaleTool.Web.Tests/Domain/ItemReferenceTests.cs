using AssociationSaleTool.Web.Domain.Items;

namespace AssociationSaleTool.Web.Tests.Domain;

public class ItemReferenceTests
{
    [Fact]
    public void FormatsReferenceWithLetterAndThreeDigitNumber()
    {
        Assert.Equal("B157", ItemReference.Format('B', 157));
    }

    [Fact]
    public void PadsShortNumbersToThreeDigits()
    {
        Assert.Equal("B001", ItemReference.Format('B', 1));
        Assert.Equal("B009", ItemReference.Format('B', 9));
        Assert.Equal("B010", ItemReference.Format('B', 10));
    }

    [Fact]
    public void PaddingKeepsLexicographicOrderEqualToNumericOrder()
    {
        var ordered = new[] { 9, 10, 100, 455 }
            .Select(number => ItemReference.Format('B', number))
            .ToList();

        Assert.Equal(ordered, ordered.OrderBy(reference => reference, StringComparer.Ordinal).ToList());
    }
}
