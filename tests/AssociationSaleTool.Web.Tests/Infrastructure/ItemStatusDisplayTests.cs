using AssociationSaleTool.Web.Infrastructure;

namespace AssociationSaleTool.Web.Tests.Infrastructure;

public sealed class ItemStatusDisplayTests
{
    [Theory]
    [InlineData("registered", "enregistré")]
    [InlineData("in_stock", "en stock")]
    [InlineData("sold", "vendu")]
    [InlineData("missing", "manquant")]
    public void ToFrenchLabel_ReturnsExpectedFrenchStatus(string status, string expectedLabel)
    {
        Assert.Equal(expectedLabel, ItemStatusDisplay.ToFrenchLabel(status));
    }

    [Fact]
    public void ToFrenchLabel_ReturnsRawStatusForRemovedLegacyStatus()
    {
        Assert.Equal("not_provided", ItemStatusDisplay.ToFrenchLabel("not_provided"));
    }
}
