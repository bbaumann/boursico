namespace AssociationSaleTool.Web.Services.Models;

public sealed record DefectiveItemCandidate(
    long ItemId,
    long SellerId,
    string SellerCode,
    string SellerName,
    string Reference,
    string Title,
    string Status,
    int SellerPriceCents,
    bool HasRefundHistory);
