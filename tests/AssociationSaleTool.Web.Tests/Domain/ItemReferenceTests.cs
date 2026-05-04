using AssociationSaleTool.Web.Domain.Items;

namespace AssociationSaleTool.Web.Tests.Domain;

public class ItemReferenceTests
{
    [Fact]
    public void FormatsReferenceWithThreeDigitItemNumber()
    {
        Assert.Equal("BK014", ItemReference.Format("BK", 14));
    }
}
