namespace AssociationSaleTool.Web.Services.Models;

public sealed record CheckoutCatalogItem(
    long Id,
    string Reference,
    string Title,
    int SellerPriceCents);
