namespace AssociationSaleTool.Web.Domain.Items;

public static class ItemReference
{
    public static string Format(string sellerCode, int itemNumber) => $"{sellerCode}{itemNumber:000}";
}
