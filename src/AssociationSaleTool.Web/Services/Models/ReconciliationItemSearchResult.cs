namespace AssociationSaleTool.Web.Services.Models;

public sealed record ReconciliationItemSearchResult(
    long ItemId,
    long SellerId,
    string SellerCode,
    string SellerName,
    string Reference,
    string Title,
    string Status,
    int SellerPriceCents);
