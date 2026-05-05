namespace AssociationSaleTool.Web.Services.Models;

public sealed record SellerSheetItem(
    long ItemId,
    string Reference,
    string Title,
    string Status,
    int PriceCents,
    bool IsMissing)
{
    public bool IsDefective => Status == "defective";
}
