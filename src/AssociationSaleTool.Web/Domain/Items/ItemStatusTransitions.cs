namespace AssociationSaleTool.Web.Domain.Items;

public static class ItemStatusTransitions
{
    public static bool CanTransition(ItemStatus from, ItemStatus to)
    {
        return (from, to) switch
        {
            (ItemStatus.Registered, ItemStatus.InStock) => true,
            (ItemStatus.Registered, ItemStatus.NotProvided) => true,
            (ItemStatus.InStock, ItemStatus.Sold) => true,
            (ItemStatus.InStock, ItemStatus.Missing) => true,
            _ => false
        };
    }
}
