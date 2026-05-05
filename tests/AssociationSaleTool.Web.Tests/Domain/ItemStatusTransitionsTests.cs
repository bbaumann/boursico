using AssociationSaleTool.Web.Domain.Items;

namespace AssociationSaleTool.Web.Tests.Domain;

public class ItemStatusTransitionsTests
{
    [Fact]
    public void ItemStatusEnum_DoesNotDefineNotProvided()
    {
        Assert.DoesNotContain("NotProvided", Enum.GetNames<ItemStatus>());
    }

    [Theory]
    [InlineData(ItemStatus.Registered, ItemStatus.InStock, true)]
    [InlineData(ItemStatus.InStock, ItemStatus.Sold, true)]
    [InlineData(ItemStatus.InStock, ItemStatus.Missing, true)]
    [InlineData(ItemStatus.InStock, ItemStatus.Defective, true)]
    [InlineData(ItemStatus.Sold, ItemStatus.InStock, false)]
    [InlineData(ItemStatus.Defective, ItemStatus.Sold, false)]
    [InlineData(ItemStatus.Registered, ItemStatus.Sold, false)]
    public void EnforcesDocumentedTransitions(ItemStatus from, ItemStatus to, bool expected)
    {
        Assert.Equal(expected, ItemStatusTransitions.CanTransition(from, to));
    }
}
