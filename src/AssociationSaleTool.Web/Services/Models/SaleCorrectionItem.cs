namespace AssociationSaleTool.Web.Services.Models;

public sealed record SaleCorrectionItem(
    long SaleLineId,
    long SaleId,
    long ItemId,
    string Reference,
    string Title,
    string SellerCode,
    string SellerName,
    int SellerPriceAtSaleCents,
    DateTime SoldAt);
