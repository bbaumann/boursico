namespace AssociationSaleTool.Web.Domain.Items;

public static class ItemDeletionPolicy
{
    public static bool CanDelete(string status)
    {
        return status is "registered" or "in_stock";
    }
}
