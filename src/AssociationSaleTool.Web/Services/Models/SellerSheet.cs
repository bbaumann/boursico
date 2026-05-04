namespace AssociationSaleTool.Web.Services.Models;

public sealed record SellerSheet(
    long SellerId,
    string SellerCode,
    string SellerName,
    string? Email,
    string Phone,
    string PostalAddress,
    IReadOnlyList<SellerSheetItem> PayableItems,
    IReadOnlyList<SellerSheetItem> ReturnedItems,
    int TotalPayableCents);
