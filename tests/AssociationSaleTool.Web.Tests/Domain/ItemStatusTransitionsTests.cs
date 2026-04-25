using AssociationSaleTool.Web.Domain.Items;

namespace AssociationSaleTool.Web.Tests.Domain;

public class ItemStatusTransitionsTests
{
    [Theory]
    [InlineData(ItemStatus.Registered, ItemStatus.InStock, true)]
    [InlineData(ItemStatus.Registered, ItemStatus.NotProvided, true)]
    [InlineData(ItemStatus.InStock, ItemStatus.Sold, true)]
    [InlineData(ItemStatus.InStock, ItemStatus.Missing, true)]
    [InlineData(ItemStatus.Sold, ItemStatus.InStock, false)]
    [InlineData(ItemStatus.Registered, ItemStatus.Sold, false)]
    public void EnforcesDocumentedTransitions(ItemStatus from, ItemStatus to, bool expected)
    {
        Assert.Equal(expected, ItemStatusTransitions.CanTransition(from, to));
    }
}
