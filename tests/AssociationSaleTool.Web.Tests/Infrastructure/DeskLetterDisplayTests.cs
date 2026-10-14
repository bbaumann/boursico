using AssociationSaleTool.Web.Infrastructure;

namespace AssociationSaleTool.Web.Tests.Infrastructure;

public sealed class DeskLetterDisplayTests
{
    [Theory]
    [InlineData(20, "Plus que 20 étiquettes en K.")]
    [InlineData(2, "Plus que 2 étiquettes en K.")]
    [InlineData(1, "Plus que 1 étiquette en K.")]
    [InlineData(0, "Plus aucune étiquette en K.")]
    public void LowLabelsWarning_AgreesWithTheCount(int labelsLeft, string expected)
    {
        Assert.Equal(expected, DeskLetterDisplay.LowLabelsWarning('K', labelsLeft));
    }

    [Theory]
    [InlineData(455, "455 étiquettes restantes")]
    [InlineData(2, "2 étiquettes restantes")]
    [InlineData(1, "1 étiquette restante")]
    [InlineData(0, "aucune étiquette restante")]
    public void LabelsLeft_AgreesWithTheCount(int labelsLeft, string expected)
    {
        Assert.Equal(expected, DeskLetterDisplay.LabelsLeft(labelsLeft));
    }
}
